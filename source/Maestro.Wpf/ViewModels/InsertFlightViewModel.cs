using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Messages;
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
    FlightMessage[] _landedFlights = [];

    [ObservableProperty]
    FlightMessage[] _pendingFlights = [];

    [ObservableProperty]
    FlightMessage? _selectedFlight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    string _callsign = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    string _aircraftType = "";

    public InsertFlightViewModel(
        string airportIdentifier,
        IInsertFlightOptions options,
        FlightMessage[] landedFlights,
        FlightMessage[] pendingFlights,
        IWindowHandle windowHandle,
        IMediator mediator,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _options = options;

        LandedFlights = landedFlights;
        PendingFlights = pendingFlights;

        _windowHandle = windowHandle;
        _mediator = mediator;
        _errorReporter = errorReporter;
    }

    partial void OnSelectedFlightChanged(FlightMessage? value)
    {
        _isUpdatingFromSelection = true;
        Callsign = value?.Callsign ?? "";
        AircraftType = value?.AircraftType ?? "";
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

    [RelayCommand]
    public void Insert()
    {
        try
        {
            // Overshoot flights get a different request
            if (LandedFlights.Any(f => f.Callsign == Callsign))
            {
                _mediator.Send(
                    new InsertOvershootRequest(
                        _airportIdentifier,
                        Callsign,
                        _options),
                    CancellationToken.None);
                return;
            }

            _mediator.Send(
                new InsertFlightRequest(
                    _airportIdentifier,
                    Callsign,
                    AircraftType,
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
