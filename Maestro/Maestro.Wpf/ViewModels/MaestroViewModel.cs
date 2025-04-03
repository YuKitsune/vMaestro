using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Dtos.Messages;
using Maestro.Wpf;
using MediatR;

namespace Maestro.Wpf;

public partial class MaestroViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AirportViewModel> _availableAirports = [];

    [ObservableProperty]
    private AirportViewModel? _selectedAirport;

    [ObservableProperty]
    private RunwayModeViewModel? _selectedRunwayMode;

    [ObservableProperty]
    private ViewConfigurationDTO? _selectedView;

    readonly IMediator _mediator;

    [ObservableProperty]
    List<AircraftViewModel> _aircraft = [];

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
            SelectedRunwayMode = null;
            SelectedView = null;
            return;
        }

        if (SelectedRunwayMode == null || !airportViewModel.RunwayModes.Any(r => r.Identifier == SelectedRunwayMode.Identifier))
        {
            SelectedRunwayMode = airportViewModel.RunwayModes.FirstOrDefault();
        }

        if (SelectedView == null || !airportViewModel.Views.Any(s => s.Identifier == SelectedView.Identifier))
        {
            SelectedView = airportViewModel.Views.FirstOrDefault();
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
    void SelectView(ViewConfigurationDTO? viewConfiguration)
    {
        SelectedView = viewConfiguration;
    }
}
