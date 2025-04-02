using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using TFMS.Core.Dtos.Messages;

namespace TFMS.Wpf;

public partial class AirportViewModel(string identifier, RunwayModeViewModel[] runwayModes, SectorViewModel[] sectors) : ObservableObject
{
    public string Identifier => identifier;

    [ObservableProperty]
    private ObservableCollection<RunwayModeViewModel> _runwayModes = new(runwayModes);

    [ObservableProperty]
    private ObservableCollection<SectorViewModel> _sectors = new(sectors);
}

public class RunwayModeViewModel(string identifier, RunwayViewModel[] runwayModes)
{
    public string Identifier => identifier;

    public RunwayViewModel[] Runways => runwayModes;
}

public partial class RunwayViewModel(string identifier, TimeSpan defaultLandingRate) : ObservableObject
{
    public string Identifier => identifier;

    public TimeSpan DefaultLandingRate => defaultLandingRate;

    [ObservableProperty]
    private TimeSpan _landingRate = defaultLandingRate;
}

public class SectorViewModel(string identifier, string[] feederFixes)
{
    public string Identifier => identifier;
    public string[] FeederFixes => feederFixes;
}

public partial class TFMSViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AirportViewModel> _availableAirports = [];

    [ObservableProperty]
    private AirportViewModel? _selectedAirport;

    [ObservableProperty]
    private RunwayModeViewModel? _selectedRunwayMode;

    [ObservableProperty]
    private SectorViewModel? _selectedSector;

    [ObservableProperty]
    private string[] _leftFeederFixes = [];

    [ObservableProperty]
    private string[] _rightFeederFixes = [];

    readonly IMediator _mediator;

    [ObservableProperty]
    List<AircraftViewModel> _aircraft = [];

    public TFMSViewModel(IMediator mediator)
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
            SelectedSector = null;
            return;
        }

        if (SelectedRunwayMode == null || !airportViewModel.RunwayModes.Any(r => r.Identifier == SelectedRunwayMode.Identifier))
        {
            SelectedRunwayMode = airportViewModel.RunwayModes.FirstOrDefault();
        }

        if (SelectedSector == null || !airportViewModel.Sectors.Any(s => s.Identifier == SelectedSector.Identifier))
        {
            SelectedSector = airportViewModel.Sectors.FirstOrDefault();
        }
    }

    partial void OnSelectedSectorChanged(SectorViewModel? sectorViewModel)
    {
        if (sectorViewModel is null)
        {
            LeftFeederFixes = [];
            RightFeederFixes = [];
            return;
        }

        double midPoint = (sectorViewModel.FeederFixes.Length + 1) / 2;
        var middleIndex = (int) Math.Ceiling(midPoint);

        LeftFeederFixes = sectorViewModel.FeederFixes.Take(middleIndex).ToArray();
        RightFeederFixes = sectorViewModel.FeederFixes.Skip(middleIndex).ToArray();
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

            var sectors = airport.Sectors.Select(s =>
                new SectorViewModel(s.Identifier, s.Fixes))
                .ToArray();

            AvailableAirports.Add(new AirportViewModel(airport.Identifier, runwayModes, sectors));
        }
    }

    [RelayCommand]
    void SelectSector(SectorViewModel? sectorViewModel)
    {
        SelectedSector = sectorViewModel;
    }
}
