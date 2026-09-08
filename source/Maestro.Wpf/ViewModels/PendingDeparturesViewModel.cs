using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

// TODO: Try to combine this with InsertFlightViewModel (and associated views)

public partial class PendingDeparturesViewModel : ObservableObject
{
    readonly string _airportIdentifier;
    readonly string[] _departureAirportIdentifiers;
    readonly IWindowHandle _windowHandle;
    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;

    bool _isUpdatingFromSelection = false;

    [ObservableProperty]
    FlightDataRecord[] _pendingFlights = [];

    [ObservableProperty]
    FlightDataRecord? _selectedFlight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    string _callsign = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    string _aircraftType = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    string _departureIdentifier = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    DateTimeOffset _takeoffTime;

    public PendingDeparturesViewModel(
        string airportIdentifier,
        string[] departureAirportIdentifiers,
        SessionDto session,
        IWindowHandle windowHandle,
        IMediator mediator,
        IClock clock,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _departureAirportIdentifiers = departureAirportIdentifiers;
        _windowHandle = windowHandle;
        _mediator = mediator;
        _errorReporter = errorReporter;

        TakeoffTime = clock.UtcNow().AddMinutes(5).Rounded();
        ApplySession(session);

        WeakReferenceMessenger.Default.Register<SessionUpdatedNotification>(this, (_, notification) =>
        {
            if (notification.AirportIdentifier != _airportIdentifier)
                return;

            ApplySession(notification.Session);
        });
    }

    void ApplySession(SessionDto session)
    {
        var activatedCallsigns = new HashSet<string>(
            session.Sequence.Flights.Select(f => f.Callsign)
                .Concat(session.DeSequencedFlights.Select(f => f.Callsign)),
            StringComparer.OrdinalIgnoreCase);

        PendingFlights = session.FlightDataRecords
            .Where(r => !activatedCallsigns.Contains(r.Callsign) &&
                        _departureAirportIdentifiers.Contains(r.Origin, StringComparer.OrdinalIgnoreCase))
            .OrderBy(r => r.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    partial void OnSelectedFlightChanged(FlightDataRecord? value)
    {
        _isUpdatingFromSelection = true;
        Callsign = value?.Callsign ?? "";
        AircraftType = value?.AircraftType ?? "";
        DepartureIdentifier = value?.Origin ?? "";
        _isUpdatingFromSelection = false;
    }

    partial void OnCallsignChanged(string _)
    {
        if (_isUpdatingFromSelection)
            return;

        SelectedFlight = null;
    }

    partial void OnAircraftTypeChanged(string _)
    {
        if (_isUpdatingFromSelection)
            return;

        SelectedFlight = null;
    }

    partial void OnDepartureIdentifierChanged(string _)
    {
        if (_isUpdatingFromSelection)
            return;

        SelectedFlight = null;
    }

    [RelayCommand(CanExecute = nameof(CanInsert))]
    public void Insert()
    {
        try
        {
            _mediator.Send(
                new InsertFlightRequest(
                    _airportIdentifier,
                    Callsign.ToUpperInvariant(),
                    AircraftType.ToUpperInvariant(),
                    new DepartureInsertionOptions(DepartureIdentifier.ToUpperInvariant(), TakeoffTime)),
                CancellationToken.None);
            CloseWindow();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    bool CanInsert()
    {
        return !string.IsNullOrEmpty(Callsign) && !string.IsNullOrEmpty(AircraftType) && !string.IsNullOrEmpty(DepartureIdentifier);
    }

    [RelayCommand]
    public void CloseWindow()
    {
        _windowHandle.Close();
    }
}
