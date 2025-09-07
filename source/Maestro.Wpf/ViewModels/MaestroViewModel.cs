using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
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
    List<FlightMessage> _flights = [];

    [ObservableProperty]
    List<SlotMessage> _slots = [];

    [ObservableProperty]
    bool _isCreatingSlot = false;

    [ObservableProperty]
    SlotCreationReferencePoint _slotCreationReferencePoint = SlotCreationReferencePoint.Before;

    [ObservableProperty]
    string[] _slotRunwayIdentifiers = [];

    [ObservableProperty]
    DateTimeOffset? _firstSlotTime = null;

    [ObservableProperty]
    DateTimeOffset? _secondSlotTime = null;

    [ObservableProperty]
    FlightMessage? _selectedFlight = null;

    [ObservableProperty]
    bool _isConfirmationDialogOpen = false;

    public string AirportIdentifier { get;}

    public string TerminalConfiguration =>
        NextRunwayMode is not null
            ? $"{CurrentRunwayMode.Identifier} → {NextRunwayMode.Identifier}"
            : CurrentRunwayMode.Identifier;

    public bool RunwayChangeIsPlanned => NextRunwayMode is not null;

    public bool HasDesequencedFlight => Flights.Any(f => f.State == State.Desequenced);

    public MaestroViewModel(
        string airportIdentifier,
        RunwayModeViewModel[] runwayModes,
        RunwayModeViewModel currentRunwayMode,
        ViewConfiguration[] views,
        FlightMessage[] flights,
        SlotMessage[] slots,
        IMediator mediator,
        IErrorReporter errorReporter)
    {
        _mediator = mediator;
        _errorReporter = errorReporter;

        AirportIdentifier = airportIdentifier;
        RunwayModes = runwayModes;
        CurrentRunwayMode = currentRunwayMode;

        Views = views;
        SelectedView = views.First();

        Flights = flights.ToList();
        Slots = slots.ToList();
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

            await _mediator.Send(request);
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
            var confirmation = await _mediator.Send(new ConfirmationRequest("Move flight", "Do you really want to move this flight?"));
            IsConfirmationDialogOpen = false;
            
            if (!confirmation.Confirmed)
                return;

            await _mediator.Send(request);
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
            await _mediator.Send(request);
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

            await _mediator.Send(new OpenSlotWindowRequest(
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

            await _mediator.Send(new OpenSlotWindowRequest(
                AirportIdentifier,
                slotMessage.SlotId,
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
                    Flights.Where(f => f.State is State.Pending).ToArray()));
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
            _mediator.Send(new OpenPendingDeparturesWindowRequest(
                AirportIdentifier,
                Flights.Where(f => f.State == State.Pending).ToArray()));
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
                    Flights.Where(f => f.State == State.Desequenced)
                        .Select(f => f.Callsign)
                        .ToArray()));
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    public void UpdateFrom(SequenceMessage sequenceMessage)
    {
        CurrentRunwayMode = new RunwayModeViewModel(sequenceMessage.CurrentRunwayMode);
        NextRunwayMode = sequenceMessage.NextRunwayMode is not null
            ? new RunwayModeViewModel(sequenceMessage.NextRunwayMode)
            : null;

        Flights = sequenceMessage.Flights.ToList();
        Slots = sequenceMessage.Slots.ToList();
    }
}
