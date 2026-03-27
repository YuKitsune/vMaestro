using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Connectivity;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Contracts.Slots;
using Maestro.Core.Configuration;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;
    readonly LabelsConfiguration? _labelsConfiguration;

    [ObservableProperty]
    ViewConfiguration[] _views = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScrollingUp), nameof(IsScrollingDown))]
    ViewConfiguration _selectedView;

    [ObservableProperty]
    RunwayModeViewModel[] _runwayModes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TerminalConfiguration))]
    [NotifyPropertyChangedFor(nameof(RunwayAchievedRates))]
    [NotifyPropertyChangedFor(nameof(RunwayIntervals))]
    RunwayModeViewModel _currentRunwayMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TerminalConfiguration))]
    [NotifyPropertyChangedFor(nameof(RunwayChangeIsPlanned))]
    RunwayModeViewModel? _nextRunwayMode;

    [ObservableProperty]
    DateTimeOffset? _runwayModeChangeTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDesequencedFlight))]
    List<FlightDto> _deSequencedFlights = [];

    [ObservableProperty]
    List<PendingFlightDto> _pendingFlights = [];

    [ObservableProperty]
    List<FlightDto> _flights = [];

    [ObservableProperty]
    List<SlotDto> _slots = [];

    [ObservableProperty]
    DateTimeOffset? _firstSlotTime;

    [ObservableProperty]
    DateTimeOffset? _secondSlotTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunwayAchievedRates))]
    LandingStatisticsDto _landingStatistics = new() { RunwayLandingTimes = new Dictionary<string, RunwayLandingTimesDto>() };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunwayAchievedRates))]
    [NotifyPropertyChangedFor(nameof(RunwayIntervals))]
    [NotifyCanExecuteChangedFor(nameof(CycleUnitsCommand))]
    LandingRateUnit _selectedUnit = LandingRateUnit.Seconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunwayAchievedRates))]
    [NotifyPropertyChangedFor(nameof(RunwayIntervals))]
    WindDto _surfaceWind = new(0,0);

    [ObservableProperty]
    WindDto _upperWind = new(0,0);

    [ObservableProperty]
    int _upperWindAltitude;

    [ObservableProperty]
    bool _manualWind;

    [ObservableProperty]
    FlightDto? _selectedFlight;

    [ObservableProperty]
    bool _isConfirmationDialogOpen;

    public string AirportIdentifier { get;}
    public string[] Runways { get; }

    public string TerminalConfiguration =>
        NextRunwayMode is not null && RunwayModeChangeTime is not null
            ? $"{CurrentRunwayMode.Identifier} -> {NextRunwayMode.Identifier} at {RunwayModeChangeTime.Value:HH:mm}"
            : CurrentRunwayMode.Identifier;

    public bool RunwayChangeIsPlanned => NextRunwayMode is not null;

    public bool HasDesequencedFlight => DeSequencedFlights.Any();

    public RunwayIntervalViewModel[] RunwayIntervals
    {
        get
        {
            if (CurrentRunwayMode == null)
                return [];

            return CurrentRunwayMode.Runways
                .Select(runway => new RunwayIntervalViewModel(
                    runway.Identifier,
                    ConvertLandingRate(runway.Identifier, TimeSpan.FromSeconds(runway.LandingRateSeconds), SelectedUnit)))
                .ToArray();
        }
    }

    public RunwayAchievedRateViewModel[] RunwayAchievedRates
    {
        get
        {
            if (CurrentRunwayMode == null || LandingStatistics == null)
                return [];

            return CurrentRunwayMode.Runways
                .Select(runway =>
                {
                    var noDeviation = new RunwayAchievedRateViewModel(runway.Identifier, "NS", "NS");
                    if (!LandingStatistics.RunwayLandingTimes.TryGetValue(runway.Identifier, out var landingTimes))
                    {
                        return noDeviation;
                    }

                    if (landingTimes.AchievedRate is AchievedRateDto achievedRateDto)
                    {
                        var achievedRateDisplay = ConvertLandingRate(runway.Identifier, achievedRateDto.AverageLandingInterval, SelectedUnit);
                        var deviationDisplay = ConvertLandingRate(runway.Identifier, achievedRateDto.LandingIntervalDeviation, SelectedUnit);

                        return new RunwayAchievedRateViewModel(
                            runway.Identifier,
                            achievedRateDisplay,
                            deviationDisplay);
                    }

                    return noDeviation;
                })
                .ToArray();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldShowFlowControls))]
    string _status = "OFFLINE";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldShowFlowControls))]
    Role _role = Role.Observer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldShowFlowControls))]
    bool _flowIsOnline = false;

    public bool ShouldShowFlowControls =>
        Status is "OFFLINE" or "READY" ||
        Role is Role.Flow or Role.Approach ||
        (Role == Role.Enroute && !FlowIsOnline);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScrolling), nameof(IsScrollingUp), nameof(IsScrollingDown))]
    TimeSpan _scrollOffset = TimeSpan.Zero;
    public bool IsScrolling => ScrollOffset != TimeSpan.Zero;
    public bool IsScrollingUp => SelectedView.Direction == TimelineDirection.Up
        ? ScrollOffset < TimeSpan.Zero
        : ScrollOffset > TimeSpan.Zero;
    public bool IsScrollingDown => SelectedView.Direction == TimelineDirection.Up
        ? ScrollOffset > TimeSpan.Zero
        : ScrollOffset < TimeSpan.Zero;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsScrollingHorizontally))]
    int _horizontalScrollOffset = 0;
    public bool IsScrollingHorizontally => HorizontalScrollOffset > 0;

    partial void OnHorizontalScrollOffsetChanged(int value)
    {
        ScrollLeftCommand.NotifyCanExecuteChanged();
        ScrollRightCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedViewChanged(ViewConfiguration value)
    {
        HorizontalScrollOffset = 0;
        ScrollRightCommand.NotifyCanExecuteChanged();
    }

    public AirportConfiguration AirportConfiguration { get; }

    public MaestroViewModel(
        string airportIdentifier,
        string[] runways,
        RunwayModeViewModel[] runwayModes,
        ViewConfiguration[] views,
        IMediator mediator,
        IErrorReporter errorReporter,
        LabelsConfiguration? labelsConfiguration,
        AirportConfiguration airportConfiguration)
    {
        AirportConfiguration = airportConfiguration;

        _mediator = mediator;
        _errorReporter = errorReporter;
        _labelsConfiguration = labelsConfiguration;

        AirportIdentifier = airportIdentifier;
        Runways = runways;
        RunwayModes = runwayModes;
        CurrentRunwayMode = runwayModes.First();
        Views = views;
        SelectedView = views.First();

        UpperWindAltitude = airportConfiguration.UpperWindAltitude;

        WeakReferenceMessenger.Default.Register<ConnectionStatusChangedNotification>(this, (r, m) =>
        {
            if (m.AirportIdentifier != AirportIdentifier)
                return;

            Status = m.Status;
            Role = m.Role;
            FlowIsOnline = m.FlowIsOnline;
        });

        WeakReferenceMessenger.Default.Register<SessionUpdatedNotification>(this, (r, notification) =>
        {
            if (notification.AirportIdentifier != AirportIdentifier)
                return;

            CurrentRunwayMode = new RunwayModeViewModel(notification.Session.Sequence.CurrentRunwayMode);
            NextRunwayMode = notification.Session.Sequence.NextRunwayMode is not null
                ? new RunwayModeViewModel(notification.Session.Sequence.NextRunwayMode)
                : null;
            RunwayModeChangeTime = notification.Session.Sequence.FirstLandingTimeForNextMode;

            DeSequencedFlights = notification.Session.DeSequencedFlights.ToList();
            PendingFlights = notification.Session.PendingFlights.ToList();
            Flights = notification.Session.Sequence.Flights.ToList();
            Slots = notification.Session.Sequence.Slots.ToList();
            LandingStatistics = notification.Session.LandingStatistics;
            SurfaceWind = notification.Session.Sequence.SurfaceWind;
            UpperWind = notification.Session.Sequence.UpperWind;
            ManualWind = notification.Session.Sequence.ManualWind;
        });
    }

    public LabelLayoutConfiguration? GetLabelLayout(ViewConfiguration view)
    {
        if (_labelsConfiguration == null)
            return null;

        return _labelsConfiguration.Layouts
            .FirstOrDefault(l => l.Identifier == view.LabelLayout);
    }

    [RelayCommand]
    void ScrollDown()
    {
        // When direction is Up, invert the scroll behavior
        ScrollOffset = SelectedView.Direction == TimelineDirection.Up
            ? ScrollOffset.Add(TimeSpan.FromMinutes(15))
            : ScrollOffset.Subtract(TimeSpan.FromMinutes(15));
    }

    [RelayCommand]
    void ScrollUp()
    {
        // When direction is Up, invert the scroll behavior
        ScrollOffset = SelectedView.Direction == TimelineDirection.Up
            ? ScrollOffset.Subtract(TimeSpan.FromMinutes(15))
            : ScrollOffset.Add(TimeSpan.FromMinutes(15));
    }

    [RelayCommand]
    void ResetScroll()
    {
        ScrollOffset = TimeSpan.Zero;
    }

    [RelayCommand(CanExecute = nameof(CanScrollLeft))]
    void ScrollLeft()
    {
        HorizontalScrollOffset--;
    }

    bool CanScrollLeft() => HorizontalScrollOffset > 0;

    [RelayCommand(CanExecute = nameof(CanScrollRight))]
    void ScrollRight()
    {
        HorizontalScrollOffset++;
    }

    bool CanScrollRight()
    {
        if (SelectedView?.Ladders == null || SelectedView.Ladders.Length < 3)
            return false;

        // Ensure at least 3 ladders remain visible after scrolling
        var remainingLadders = SelectedView.Ladders.Length - ((HorizontalScrollOffset + 1) * 2);
        return remainingLadders > 0;
    }

    [RelayCommand]
    async Task MoveFlightWithoutConfirmation(MoveFlightRequest request)
    {
        try
        {
            await _mediator.Send(request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    async Task MoveFlightWithConfirmation(MoveFlightRequest request)
    {
        try
        {
            IsConfirmationDialogOpen = true;
            var confirmation = await _mediator.Send(new ConfirmationRequest("Move flight", "Do you really want to move this flight?"));
            IsConfirmationDialogOpen = false;

            if (!confirmation.Confirmed)
                return;

            await _mediator.Send(request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            IsConfirmationDialogOpen = false;
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    async Task SwapFlights(SwapFlightsRequest request)
    {
        try
        {
            await _mediator.Send(request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    async Task MakeStable(MakeStableRequest request)
    {
        try
        {
            await _mediator.Send(request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    public async void ShowSlotWindow(DateTimeOffset startTime, DateTimeOffset endTime, string[] runwayIdentifiers)
    {
        try
        {
            if (string.IsNullOrEmpty(AirportIdentifier)) return;

            await _mediator.Send(
                new OpenSlotWindowRequest(
                    AirportIdentifier,
                    null, // slotId is null for new slots
                    startTime,
                    endTime,
                    runwayIdentifiers));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    public async void ShowSlotWindow(SlotDto slotDto)
    {
        try
        {
            if (string.IsNullOrEmpty(AirportIdentifier))
                return;

            await _mediator.Send(
                new OpenSlotWindowRequest(
                    AirportIdentifier,
                    slotDto.Id,
                    slotDto.StartTime,
                    slotDto.EndTime,
                    slotDto.RunwayIdentifiers));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    public void ShowInsertFlightWindow(IInsertFlightOptions options)
    {
        try
        {
            if (string.IsNullOrEmpty(AirportIdentifier))
                return;

            _mediator.Send(
                new OpenInsertFlightWindowRequest(
                    AirportIdentifier,
                    options,
                    Flights.Where(f => f.State is State.Landed).ToArray(),
                    PendingFlights.ToArray()));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    public void SelectFlight(FlightDto flight)
    {
        SelectedFlight = flight;
    }

    public void DeselectFlight()
    {
        SelectedFlight = null;
    }

    [RelayCommand]
    void OpenTerminalConfiguration()
    {
        try
        {
            _mediator.Send(new OpenTerminalConfigurationRequest(AirportIdentifier));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void SelectView(ViewConfiguration viewConfiguration)
    {
        SelectedView = viewConfiguration;
    }

    [RelayCommand]
    void OpenPendingDeparturesWindow()
    {
        try
        {
            _mediator.Send(
                new OpenPendingDeparturesWindowRequest(
                    AirportIdentifier,
                    PendingFlights.ToArray()));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void OpenDesequencedWindow()
    {
        try
        {
            _mediator.Send(
                new OpenDesequencedWindowRequest(
                    AirportIdentifier,
                    DeSequencedFlights
                        .Select(f => f.Callsign)
                        .ToArray()));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void OpenConnectionWindow()
    {
        try
        {
            _mediator.Send(new OpenConnectionWindowRequest(AirportIdentifier));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void OpenCoordinationWindow()
    {
        try
        {
            _mediator.Send(new OpenCoordinationWindowRequest(AirportIdentifier, null));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void OpenWind()
    {
        try
        {
            _mediator.Send(
                new OpenWindWindowRequest(
                    AirportIdentifier,
                    SurfaceWind,
                    UpperWind,
                    UpperWindAltitude));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    void CycleUnits()
    {
        SelectedUnit = SelectedUnit switch
        {
            LandingRateUnit.Seconds => LandingRateUnit.NauticalMiles,
            LandingRateUnit.NauticalMiles => LandingRateUnit.AircraftPerHour,
            LandingRateUnit.AircraftPerHour => LandingRateUnit.Seconds,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    string ConvertLandingRate(string runwayIdentifier, TimeSpan interval, LandingRateUnit unit)
    {
        switch (unit)
        {
            case LandingRateUnit.Seconds:
                return  $"{(int)interval.TotalSeconds}";

            case LandingRateUnit.NauticalMiles:
            {
                var runwayDirection = int.Parse(runwayIdentifier.Substring(0, 2)) * 10; // Checky hack to get the runway direction
                var angle = Math.Abs(SurfaceWind.Direction - runwayDirection);
                var headwindComponent = SurfaceWind.Speed * Math.Cos(angle);

                var groundSpeed = AirportConfiguration.AverageLandingSpeed - headwindComponent;
                var distance = groundSpeed * interval.TotalHours;
                return distance.ToString("0.0");
            }

            case LandingRateUnit.AircraftPerHour:
                return ((int)(1 / interval.TotalHours)).ToString();

            default:
                return "?";
        }
    }
}
