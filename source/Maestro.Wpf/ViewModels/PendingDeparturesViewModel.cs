using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Wpf.ViewModels;

// TODO: Try to combine this with InsertFlightViewModel (and associated views)

public partial class PendingDeparturesViewModel : ObservableObject
{
    readonly string _airportIdentifier;
    readonly IWindowHandle _windowHandle;
    readonly IMediator _mediator;
    readonly IErrorReporter _errorReporter;

    bool _isUpdatingFromSelection = false;

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    string _departureIdentifier = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InsertCommand))]
    DateTimeOffset _takeoffTime;

    public PendingDeparturesViewModel(
        string airportIdentifier,
        FlightMessage[] pendingFlights,
        IWindowHandle windowHandle,
        IMediator mediator,
        IClock clock,
        IErrorReporter errorReporter)
    {
        _airportIdentifier = airportIdentifier;
        _windowHandle = windowHandle;
        _mediator = mediator;
        _errorReporter = errorReporter;

        PendingFlights = pendingFlights;
        TakeoffTime = clock.UtcNow().AddMinutes(5).Rounded();
    }

    partial void OnSelectedFlightChanged(FlightMessage? value)
    {
        _isUpdatingFromSelection = true;
        Callsign = value?.Callsign ?? "";
        AircraftType = value?.AircraftType ?? "";
        DepartureIdentifier = value?.OriginIdentifier ?? "";
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
            _mediator.Send(new InsertDepartureRequest(
                _airportIdentifier,
                Callsign,
                AircraftType,
                DepartureIdentifier,
                TakeoffTime));
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
