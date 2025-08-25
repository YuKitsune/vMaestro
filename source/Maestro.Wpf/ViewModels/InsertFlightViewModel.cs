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
        IMediator mediator)
    {
        _airportIdentifier = airportIdentifier;
        _options = options;

        LandedFlights = landedFlights;
        PendingFlights = pendingFlights;

        _windowHandle = windowHandle;
        _mediator = mediator;
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
        _mediator.Send(
            new InsertFlightRequest(
                _airportIdentifier,
                Callsign,
                AircraftType,
                _options));

        CloseWindow();
    }

    [RelayCommand]
    public void CloseWindow()
    {
        _windowHandle.Close();
    }
}
