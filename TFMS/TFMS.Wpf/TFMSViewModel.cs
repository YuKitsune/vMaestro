using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TFMS.Core;

namespace TFMS.Wpf;

public class AirportViewModel
{
    public string Identifier { get; set; }
}

public class RunwayModeViewModel
{
    public string Identifier { get; set; }

    public RunwayViewModel[] Runways { get; set; }

}

public class RunwayViewModel
{
    public string Identifier { get; set; }
    public TimeSpan LandingRate { get; set; }
}

public class SectorViewModel
{
    public string Identifier { get; set; }
    public string[] FeederFixes { get; set; }
}

public partial class TFMSViewModel : ObservableObject
{
    [ObservableProperty]
    private AirportViewModel[] _availableAirports = [];

    [ObservableProperty]
    private AirportViewModel? _selectedAirport;

    [ObservableProperty]
    private RunwayModeViewModel[] _availableRunwayModes = [];

    [ObservableProperty]
    private RunwayModeViewModel? _selectedRunwayMode;

    [ObservableProperty]
    private RunwayViewModel[] _runwayRates;

    [ObservableProperty]
    private SectorViewModel[] _availableSectors = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LeftFeederFixes))]
    [NotifyPropertyChangedFor(nameof(RightFeederFixes))]
    private SectorViewModel? _selectedSector;

    public string[] LeftFeederFixes => SelectedSector is null ? [] : SelectedSector.FeederFixes.Take(SelectedSector.FeederFixes.Length / 2).ToArray();
    public string[] RightFeederFixes => SelectedSector is null ? [] : SelectedSector.FeederFixes.Skip(SelectedSector.FeederFixes.Length / 2).ToArray();

    [ObservableProperty]
    List<AircraftViewModel> _aircraft = [];

    public TFMSViewModel()
    {
        AvailableAirports = Configuration.Demo.Airports
            .Select(a => new AirportViewModel { Identifier = a.Identifier })
            .ToArray();
    }

    partial void OnAvailableAirportsChanged(AirportViewModel[] availableAirports)
    {
        if (SelectedAirport == null || !availableAirports.Any(a => a.Identifier == SelectedAirport.Identifier))
        {
            SelectedAirport = availableAirports.FirstOrDefault();
        }
    }

    partial void OnSelectedAirportChanged(AirportViewModel airportViewModel)
    {
        var airport = Configuration.Demo.Airports
            .First(a => a.Identifier == airportViewModel.Identifier);

        AvailableRunwayModes = airport
            .RunwayModes.Select(rm =>
                new RunwayModeViewModel
                { 
                    Identifier = rm.Identifier,
                    Runways = rm.RunwayRates.Select(r =>
                        new RunwayViewModel
                        {
                            Identifier = r.RunwayIdentifier,
                            LandingRate = r.LandingRate,
                        }).ToArray()
                })
            .ToArray();

        AvailableSectors = airport.Sectors
            .Select(s =>
                new SectorViewModel
                {
                    Identifier = s.Identifier,
                    FeederFixes = s.Fixes
                })
            .ToArray();
    }

    partial void OnAvailableRunwayModesChanged(RunwayModeViewModel[] availableRunwayModes)
    {
        if (SelectedRunwayMode == null || !availableRunwayModes.Any(r => r.Identifier == SelectedRunwayMode.Identifier))
        {
            SelectedRunwayMode = availableRunwayModes.FirstOrDefault();
        }
    }

    partial void OnAvailableSectorsChanged(SectorViewModel[] availableSectors)
    {
        if (SelectedSector == null || !availableSectors.Any(s => s.Identifier == SelectedSector.Identifier))
        {
            SelectedSector = availableSectors.FirstOrDefault();
        }
    }

    partial void OnSelectedRunwayModeChanged(RunwayModeViewModel runwayModeViewModel)
    {
        RunwayRates = Configuration.Demo.Airports
            .First(a => a.Identifier == SelectedAirport.Identifier)
            .RunwayModes.First(r => r.Identifier == runwayModeViewModel.Identifier)
            .RunwayRates.Select(r =>
                new RunwayViewModel
                {
                    Identifier = r.RunwayIdentifier,
                    LandingRate = r.LandingRate
                })
            .ToArray();
    }

    [RelayCommand]
    void SelectSector(SectorViewModel sectorViewModel)
    {
        SelectedSector = sectorViewModel;
    }
}
