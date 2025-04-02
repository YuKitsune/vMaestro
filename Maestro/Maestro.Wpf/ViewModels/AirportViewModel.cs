using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Maestro.Wpf;

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