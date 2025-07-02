using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel : ObservableObject
{
    readonly IMediator _mediator;
    
    [ObservableProperty]
    ObservableCollection<AirportViewModel> _availableAirports = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDesequencedWindowCommand))]
    AirportViewModel? _selectedAirport;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TerminalConfiguration))]
    RunwayModeViewModel? _currentRunwayMode;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TerminalConfiguration))]
    [NotifyPropertyChangedFor(nameof(RunwayChangeIsPlanned))]
    RunwayModeViewModel? _nextRunwayMode;

    [ObservableProperty]
    ViewConfiguration? _selectedView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Desequenced))]
    List<FlightViewModel> _flights = [];

    public string TerminalConfiguration
    {
        get
        {
            if (CurrentRunwayMode is null)
                return "NONE";

            if (NextRunwayMode is not null)
                return $"{CurrentRunwayMode.Identifier} → {NextRunwayMode.Identifier}";

            return CurrentRunwayMode.Identifier;
        }
    }

    public bool RunwayChangeIsPlanned => NextRunwayMode is not null;

    public string[] Desequenced => Flights.Where(f => f.State == State.Desequenced).Select(airport => airport.Callsign).ToArray();

    public MaestroViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    partial void OnAvailableAirportsChanged(ObservableCollection<AirportViewModel> availableAirports)
    {
        // Select the first airport if the selected one no longer exists
        if (SelectedAirport == null || !availableAirports.Any(a => a.Identifier == SelectedAirport.Identifier))
        {
            SelectedAirport = availableAirports.FirstOrDefault();
        }
    }

    partial void OnSelectedAirportChanged(AirportViewModel? airportViewModel)
    {
        if (airportViewModel is null)
        {
            CurrentRunwayMode = null;
            SelectedView = null;
            return;
        }

        // TODO: Select active runway mode
        if (CurrentRunwayMode == null || airportViewModel.RunwayModes.All(r => r.Identifier != CurrentRunwayMode.Identifier))
        {
            CurrentRunwayMode = airportViewModel.RunwayModes.FirstOrDefault();
        }

        // TODO: Remember selected airport
        if (SelectedView == null || airportViewModel.Views.All(s => s.Identifier != SelectedView.Identifier))
        {
            SelectedView = airportViewModel.Views.FirstOrDefault();
        }

        var response = _mediator.Send(new GetSequenceRequest(airportViewModel.Identifier)).GetAwaiter().GetResult();
        foreach (var flight in response.Sequence.Flights)
        {
            UpdateFlight(flight);
        }
    }

    [RelayCommand]
    async Task LoadConfiguration()
    {
        var response = await _mediator.Send(new GetAirportConfigurationRequest(), CancellationToken.None);

        AvailableAirports.Clear();

        foreach (var airport in response.Airports)
        {
            var runwayModes = airport.RunwayModes.Select(rm =>
                new RunwayModeViewModel(
                    rm.Identifier,
                    rm.Runways.Select(r =>
                        new RunwayViewModel(r.Identifier, r.LandingRateSeconds))
                    .ToArray()))
                .ToArray();

            AvailableAirports.Add(new AirportViewModel(airport.Identifier, runwayModes, airport.Views));
        }
    }

    [RelayCommand]
    void OpenTerminalConfiguration()
    {
        if (SelectedAirport is null)
            return;
        
        _mediator.Send(new OpenTerminalConfigurationRequest(SelectedAirport.Identifier));
    }

    [RelayCommand]
    void SelectView(ViewConfiguration? viewConfiguration)
    {
        SelectedView = viewConfiguration;
    }
    
    [RelayCommand(CanExecute = nameof(CanOpenDesequencedWindow))]
    void OpenDesequencedWindow() => _mediator.Send(new OpenDesequencedWindowRequest(SelectedAirport!.Identifier, Desequenced));
    bool CanOpenDesequencedWindow() => SelectedAirport is not null;

    public void UpdateFlight(FlightMessage flight)
    {
        var flights = Flights.ToList();
        var index = flights.FindIndex(f => f.Callsign == flight.Callsign);
        var viewModel = new FlightViewModel(flight);
        if (index != -1)
        {
            flights[index] = viewModel;
        }
        else
        {
            flights.Add(viewModel);
        }

        Flights = flights;
    }
}
