using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;

    [ObservableProperty]
    ViewConfiguration[] _views = [];

    [ObservableProperty]
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
    [NotifyPropertyChangedFor(nameof(HasDesequencedFlight))]
    List<FlightMessage> _deSequencedFlights = [];

    [ObservableProperty]
    List<FlightMessage> _pendingFlights = [];

    [ObservableProperty]
    List<FlightMessage> _flights = [];

    [ObservableProperty]
    List<SlotMessage> _slots = [];

    [ObservableProperty]
    bool _isCreatingSlot;

    [ObservableProperty]
    SlotCreationReferencePoint _slotCreationReferencePoint = SlotCreationReferencePoint.Before;

    [ObservableProperty]
    string[] _slotRunwayIdentifiers = [];

    [ObservableProperty]
    DateTimeOffset? _firstSlotTime;

    [ObservableProperty]
    DateTimeOffset? _secondSlotTime;

    [ObservableProperty]
    FlightMessage? _selectedFlight;

    [ObservableProperty]
    bool _isConfirmationDialogOpen;

    public string AirportIdentifier { get;}
    public string[] Runways { get; }
    public Dictionary<string, string[]> ApproachTypes { get; }

    public string TerminalConfiguration =>
        NextRunwayMode is not null
            ? $"{CurrentRunwayMode.Identifier} → {NextRunwayMode.Identifier}"
            : CurrentRunwayMode.Identifier;

    public bool RunwayChangeIsPlanned => NextRunwayMode is not null;

    public bool HasDesequencedFlight => DeSequencedFlights.Any();

    [ObservableProperty] string _status = "OFFLINE";

    public MaestroViewModel(
        string airportIdentifier,
        string[] runways,
        Dictionary<string, string[]> approachTypes,
        RunwayModeViewModel[] runwayModes,
        ViewConfiguration[] views,
        IMediator mediator,
        IErrorReporter errorReporter)
    {
        _mediator = mediator;
        _errorReporter = errorReporter;

        AirportIdentifier = airportIdentifier;
        Runways = runways;
        ApproachTypes = approachTypes;
        RunwayModes = runwayModes;
        CurrentRunwayMode = runwayModes.First();
        Views = views;
        SelectedView = views.First();

        WeakReferenceMessenger.Default.Register<ConnectionStatusChanged>(this, (r, m) =>
        {
            if (m.AirportIdentifier == AirportIdentifier)
            {
                Status = m.Status;
            }
        });

        WeakReferenceMessenger.Default.Register<SequenceUpdatedNotification>(this, (r, notification) =>
        {
            if (notification.AirportIdentifier != AirportIdentifier)
                return;

            CurrentRunwayMode = new RunwayModeViewModel(notification.Sequence.CurrentRunwayMode);
            NextRunwayMode = notification.Sequence.NextRunwayMode is not null
                ? new RunwayModeViewModel(notification.Sequence.NextRunwayMode)
                : null;

            DeSequencedFlights = notification.Sequence.DeSequencedFlights.ToList();
            PendingFlights = notification.Sequence.PendingFlights.ToList();
            Flights = notification.Sequence.Flights.ToList();
            Slots = notification.Sequence.Slots.ToList();
        });
    }

    [RelayCommand]
    async Task MoveFlight(MoveFlightRequest request)
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
            IsConfirmationDialogOpen = true;
            var confirmation = await _mediator.Send(
                new ConfirmationRequest("Move flight", "Do you really want to move this flight?"));
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

    public void BeginSlotCreation(DateTimeOffset firstSlotTime, SlotCreationReferencePoint slotCreationReferencePoint, string[] runwayIdentifiers)
    {
        IsCreatingSlot = true;
        SlotCreationReferencePoint = slotCreationReferencePoint;

        // Round time based on reference point:
        // Before: round down to the previous minute
        // After: round up to the next minute
        FirstSlotTime = slotCreationReferencePoint == SlotCreationReferencePoint.Before
            ? new DateTimeOffset(firstSlotTime.Year, firstSlotTime.Month, firstSlotTime.Day,
                                firstSlotTime.Hour, firstSlotTime.Minute, 0, firstSlotTime.Offset)
            : new DateTimeOffset(firstSlotTime.Year, firstSlotTime.Month, firstSlotTime.Day,
                                firstSlotTime.Hour, firstSlotTime.Minute, 0, firstSlotTime.Offset).AddMinutes(1);
        SlotRunwayIdentifiers = runwayIdentifiers;
    }

    public void EndSlotCreation(DateTimeOffset secondSlotTime)
    {
        IsCreatingSlot = false;
        SecondSlotTime = secondSlotTime.Rounded();

        var startTime = FirstSlotTime!.Value.IsSameOrBefore(SecondSlotTime.Value) ? FirstSlotTime.Value : SecondSlotTime.Value;
        var endTime = FirstSlotTime!.Value.IsSameOrBefore(SecondSlotTime.Value) ? SecondSlotTime.Value : FirstSlotTime.Value;

        ShowSlotWindow(startTime, endTime, SlotRunwayIdentifiers);
    }

    async void ShowSlotWindow(DateTimeOffset startTime, DateTimeOffset endTime, string[] runwayIdentifiers)
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

    public async void ShowSlotWindow(SlotMessage slotMessage)
    {
        try
        {
            if (string.IsNullOrEmpty(AirportIdentifier))
                return;

            await _mediator.Send(
                new OpenSlotWindowRequest(
                    AirportIdentifier,
                    slotMessage.Id,
                    slotMessage.StartTime,
                    slotMessage.EndTime,
                    slotMessage.RunwayIdentifiers));
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

    public void SelectFlight(FlightMessage flight)
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
}
