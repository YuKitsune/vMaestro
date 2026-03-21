using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
    List<FlightDto> _pendingFlights = [];

    [ObservableProperty]
    List<FlightDto> _flights = [];

    [ObservableProperty]
    List<SlotDto> _slots = [];

    [ObservableProperty]
    DateTimeOffset? _firstSlotTime;

    [ObservableProperty]
    DateTimeOffset? _secondSlotTime;

    [ObservableProperty]
    FlightDto? _selectedFlight;

    [ObservableProperty]
    bool _isConfirmationDialogOpen;

    public string AirportIdentifier { get;}
    public string[] Runways { get; }

    public string TerminalConfiguration =>
        NextRunwayMode is not null && RunwayModeChangeTime is not null
            ? $"{CurrentRunwayMode.Identifier} → {NextRunwayMode.Identifier} at {RunwayModeChangeTime.Value:HH:mm}"
            : CurrentRunwayMode.Identifier;

    public bool RunwayChangeIsPlanned => NextRunwayMode is not null;

    public bool HasDesequencedFlight => DeSequencedFlights.Any();

    [ObservableProperty] string _status = "OFFLINE";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsScrolling), nameof(IsScrollingUp), nameof(IsScrollingDown))]
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

        WeakReferenceMessenger.Default.Register<ConnectionStatusChangedNotification>(this, (r, m) =>
        {
            if (m.AirportIdentifier == AirportIdentifier)
            {
                Status = m.Status;
            }
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
}
