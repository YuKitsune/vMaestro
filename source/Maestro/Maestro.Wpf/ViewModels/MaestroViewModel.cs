using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel : ObservableObject
{
    readonly IMediator _mediator;
    readonly IClock _clock;
    
    [ObservableProperty]
    ObservableCollection<AirportViewModel> _availableAirports = [];

    [ObservableProperty]
    AirportViewModel? _selectedAirport;

    [ObservableProperty]
    RunwayModeViewModel? _selectedRunwayMode;

    [ObservableProperty]
    ViewConfiguration? _selectedView;

    [ObservableProperty]
    List<FlightViewModel> _aircraft = [];

    public MaestroViewModel(IMediator mediator, IClock clock)
    {
        _mediator = mediator;
        _clock = clock;
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
            SelectedRunwayMode = null;
            SelectedView = null;
            return;
        }

        if (SelectedRunwayMode == null || airportViewModel.RunwayModes.All(r => r.Identifier != SelectedRunwayMode.Identifier))
        {
            SelectedRunwayMode = airportViewModel.RunwayModes.FirstOrDefault();
        }

        if (SelectedView == null || airportViewModel.Views.All(s => s.Identifier != SelectedView.Identifier))
        {
            SelectedView = airportViewModel.Views.FirstOrDefault();
        }

        var response = _mediator.Send(new GetSequenceRequest(SelectedAirport.Identifier)).GetAwaiter().GetResult();
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
                        new RunwayViewModel(r.Identifier, TimeSpan.FromSeconds(r.DefaultLandingRateSeconds)))
                    .ToArray()))
                .ToArray();

            AvailableAirports.Add(new AirportViewModel(airport.Identifier, runwayModes, airport.Views));
        }
    }

    [RelayCommand]
    void SelectView(ViewConfiguration? viewConfiguration)
    {
        SelectedView = viewConfiguration;
    }

    public void UpdateFlight(Flight flight)
    {
        var aircraft = Aircraft.ToList();
        
        var index = aircraft.FindIndex(f => f.Callsign == flight.Callsign);
        var viewModel = new FlightViewModel(
            flight.Callsign,
            flight.AircraftType,
            flight.WakeCategory,
            flight.OriginIdentifier,
            flight.DestinationIdentifier,
            flight.State,
            -1, // TODO:
            flight.FeederFixIdentifier,
            flight.InitialFeederFixTime,
            flight.EstimatedFeederFixTime,
            flight.ScheduledFeederFixTime,
            flight.AssignedRunwayIdentifier,
            -1, // TODO:
            flight.InitialLandingTime,
            flight.EstimatedLandingTime,
            flight.ScheduledLandingTime,
            flight.TotalDelay,
            flight.RemainingDelay,
            flight.FlowControls);
        
        if (index != -1)
        {
            aircraft[index] = viewModel;
        }
        else
        {
            aircraft.Add(viewModel);
        }

        Aircraft = aircraft;
    }

    partial void OnSelectedRunwayModeChanged(RunwayModeViewModel? runwayMode)
    {
        if (SelectedAirport is null || runwayMode is null)
            return;
        
        // TODO: Ask if runways should be re-assigned
        _mediator.Send(
            new ChangeRunwayModeRequest(
                SelectedAirport.Identifier,
                runwayMode.Identifier,
                _clock.UtcNow(),
                false));
    }
}
