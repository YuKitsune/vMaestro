using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;

namespace Maestro.Wpf.ViewModels;

public partial class AirportViewModel(string identifier, RunwayModeViewModel[] runwayModes, ViewConfiguration[] views) : ObservableObject
{
    public string Identifier => identifier;

    [ObservableProperty]
    ObservableCollection<RunwayModeViewModel> _runwayModes = new(runwayModes);

    [ObservableProperty]
    ObservableCollection<ViewConfiguration> _views = new(views);
}

public partial class RunwayModeViewModel : ObservableObject
{
    [ObservableProperty]
    RunwayViewModel[] _runways = [];
    
    public RunwayModeViewModel(RunwayModeDto runwayModeDto)
        : this(runwayModeDto.Identifier, runwayModeDto.Runways.Select(r => new RunwayViewModel(r)).ToArray())
    {
    }
    
    public RunwayModeViewModel(string identifier, RunwayViewModel[] runways)
    {
        Identifier = identifier;
        Runways = runways;
    }
    
    public string Identifier { get; }
}

public partial class RunwayViewModel : ObservableObject
{
    [ObservableProperty]
    int _landingRateSeconds;

    public RunwayViewModel(RunwayConfigurationDto runwayConfigurationDto)
        : this(runwayConfigurationDto.RunwayIdentifier, runwayConfigurationDto.AcceptanceRate)
    {
    }

    public RunwayViewModel(string identifier, int landingRateSeconds)
    {
        Identifier = identifier;
        LandingRateSeconds = landingRateSeconds;
    }
    
    public string Identifier { get; }
}
