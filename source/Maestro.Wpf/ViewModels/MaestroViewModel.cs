using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel : ObservableObject, IAsyncDisposable
{
    readonly IMessageDispatcher _messageDispatcher;
    readonly IErrorReporter _errorReporter;
    readonly INotificationStream<SequenceUpdatedNotification> _sequenceUpdatedNotificationStream;

    readonly CancellationTokenSource _notificationSubscriptionCancellationTokenSource;
    readonly Task _notificationSubscriptionTask;

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
        IMessageDispatcher messageDispatcher,
        IErrorReporter errorReporter,
        INotificationStream<SequenceUpdatedNotification> sequenceUpdatedNotificationStream)
    {
        _messageDispatcher = messageDispatcher;
        _errorReporter = errorReporter;
        _sequenceUpdatedNotificationStream = sequenceUpdatedNotificationStream;

        AirportIdentifier = airportIdentifier;
        _runwayModes = runwayModes;
        _currentRunwayMode = currentRunwayMode;
        RunwayModes = runwayModes;
        CurrentRunwayMode = currentRunwayMode;

        Views = views;
        SelectedView = views.First();

        Flights = flights.ToList();
        Slots = slots.ToList();

        // Subscribe to notifications
        _notificationSubscriptionCancellationTokenSource = new CancellationTokenSource();
        _notificationSubscriptionTask = SubscribeToNotifications(_notificationSubscriptionCancellationTokenSource.Token);
    }

    [RelayCommand]
    async Task MoveFlight(MoveFlightRequest request)
    {
        try
        {
            IsConfirmationDialogOpen = true;
            var confirmation = await _messageDispatcher.Send(
                new ConfirmationRequest("Move flight", "Do you really want to move this flight?"),
                CancellationToken.None);
            IsConfirmationDialogOpen = false;

            if (!confirmation.Confirmed)
                return;

            await _messageDispatcher.Send(request);
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
            var confirmation = await _messageDispatcher.Send(
                new ConfirmationRequest("Move flight", "Do you really want to move this flight?"),
                CancellationToken.None);
            IsConfirmationDialogOpen = false;

            if (!confirmation.Confirmed)
                return;

            await _messageDispatcher.Send(request);
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
            await _messageDispatcher.Send(request, CancellationToken.None);
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

            await _messageDispatcher.Send(
                new OpenSlotWindowRequest(
                    AirportIdentifier,
                    null, // slotId is null for new slots
                    startTime,
                    endTime,
                    runwayIdentifiers),
                CancellationToken.None);
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

            await _messageDispatcher.Send(
                new OpenSlotWindowRequest(
                    AirportIdentifier,
                    slotMessage.SlotId,
                    slotMessage.StartTime,
                    slotMessage.EndTime,
                    slotMessage.RunwayIdentifiers),
                CancellationToken.None);
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

            _messageDispatcher.Send(
                new OpenInsertFlightWindowRequest(
                    AirportIdentifier,
                    options,
                    Flights.Where(f => f.State is State.Landed).ToArray(),
                    Flights.Where(f => f.State is State.Pending).ToArray()),
                CancellationToken.None);
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
            _messageDispatcher.Send(new OpenTerminalConfigurationRequest(AirportIdentifier), CancellationToken.None);
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
            _messageDispatcher.Send(
                new OpenPendingDeparturesWindowRequest(
                    AirportIdentifier,
                    Flights.Where(f => f.State == State.Pending).ToArray()),
                CancellationToken.None);
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
            _messageDispatcher.Send(
                new OpenDesequencedWindowRequest(
                    AirportIdentifier,
                    Flights.Where(f => f.State == State.Desequenced)
                        .Select(f => f.Callsign)
                        .ToArray()),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    async Task SubscribeToNotifications(CancellationToken cancellationToken)
    {
        await foreach (var notification in _sequenceUpdatedNotificationStream.SubscribeAsync(cancellationToken))
        {
            try
            {
                if (notification.AirportIdentifier != AirportIdentifier)
                    continue;

                CurrentRunwayMode = new RunwayModeViewModel(notification.Sequence.CurrentRunwayMode);
                NextRunwayMode = notification.Sequence.NextRunwayMode is not null
                    ? new RunwayModeViewModel(notification.Sequence.NextRunwayMode)
                    : null;

                Flights = notification.Sequence.Flights.ToList();
                Slots = notification.Sequence.Slots.ToList();
            }
            catch (Exception ex)
            {
                _errorReporter.ReportError(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _notificationSubscriptionCancellationTokenSource.Cancel();
        await _notificationSubscriptionTask;
    }
}
