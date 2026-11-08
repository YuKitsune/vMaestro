using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

// TODO: Try to combine this with PendingDeparturesViewModel (and associated views)

public partial class InsertFlightViewModel : ObservableObject
{
    readonly IWindowHandle _windowHandle;
    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;

    readonly string _airportIdentifier;
    readonly IInsertFlightOptions _options;

    bool _isUpdatingFromSelection = false;

    [ObservableProperty]
    FlightDto[] _landedFlights = [];

    [ObservableProperty]
    FlightDataRecord[] _pendingFlights = [];

    [ObservableProperty]
    FlightDto? _selectedLandedFlight;

    [ObservableProperty]
    FlightDataRecord? _selectedFlight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    string _callsign = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    string _aircraftType = "";

    public InsertFlightViewModel(
        string airportIdentifier,
        IInsertFlightOptions options,
        SessionDto session,
        IWindowHandle windowHandle,
        IMediator mediator,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _options = options;
        _windowHandle = windowHandle;
        _mediator = mediator;
        _errorReporter = errorReporter;

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
        LandedFlights = session.Sequence.Flights
            .Where(f => f.State is State.Landed)
            .OrderByDescending(f => f.LandingTime)
            .ToArray();

        var activatedCallsigns = new HashSet<string>(
            session.Sequence.Flights.Select(f => f.Callsign)
                .Concat(session.DeSequencedFlights.Select(f => f.Callsign)),
            StringComparer.OrdinalIgnoreCase);

        PendingFlights = session.FlightDataRecords
            .Where(r => !activatedCallsigns.Contains(r.Callsign))
            .OrderBy(r => r.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // Landed flights are still in the sequence. Inserting one moves it back into the sequence at the requested time.
    partial void OnSelectedLandedFlightChanged(FlightDto? value)
    {
        if (_isUpdatingFromSelection)
            return;

        _isUpdatingFromSelection = true;
        SelectedFlight = null;
        Callsign = value?.Callsign ?? "";
        AircraftType = value?.AircraftType ?? "";
        _isUpdatingFromSelection = false;
    }

    partial void OnSelectedFlightChanged(FlightDataRecord? value)
    {
        if (_isUpdatingFromSelection)
            return;

        _isUpdatingFromSelection = true;
        SelectedLandedFlight = null;
        Callsign = value?.Callsign ?? "";
        AircraftType = value?.AircraftType ?? "";
        _isUpdatingFromSelection = false;
    }

    partial void OnCallsignChanged(string _)
    {
        if (_isUpdatingFromSelection)
            return;

        ClearSelection();
    }

    partial void OnAircraftTypeChanged(string _)
    {
        if (_isUpdatingFromSelection)
            return;

        ClearSelection();
    }

    void ClearSelection()
    {
        _isUpdatingFromSelection = true;
        SelectedLandedFlight = null;
        SelectedFlight = null;
        _isUpdatingFromSelection = false;
    }

    [RelayCommand]
    public async Task Insert()
    {
        try
        {
            await _mediator.Send(
                new InsertFlightRequest(
                    _airportIdentifier,
                    Callsign.ToUpperInvariant(),
                    AircraftType.ToUpperInvariant(),
                    _options),
                CancellationToken.None);

            CloseWindow();
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
    }

    [RelayCommand]
    public void CloseWindow()
    {
        _windowHandle.Close();
    }
}
