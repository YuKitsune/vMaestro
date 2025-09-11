using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Configuration;
using Maestro.Core.Messages;

namespace Maestro.Wpf.ViewModels;

public partial class AirportViewModel(
    string identifier,
    RunwayModeViewModel[] runwayModes,
    RunwayModeViewModel currentRunwayMode,
    ViewConfiguration[] views)
    : ObservableObject
{
    public string Identifier => identifier;

    [ObservableProperty]
    ObservableCollection<RunwayModeViewModel> _runwayModes = new(runwayModes);

    [ObservableProperty]
    RunwayModeViewModel _currentRunwayMode = currentRunwayMode;

    [ObservableProperty]
    ObservableCollection<ViewConfiguration> _views = new(views);
}

public partial class RunwayModeViewModel : ObservableObject
{
    [ObservableProperty]
    RunwayViewModel[] _runways = [];

    public RunwayModeViewModel(RunwayModeConfiguration runwayModeConfiguration)
        : this(runwayModeConfiguration.Identifier, runwayModeConfiguration.Runways.Select(r => new RunwayViewModel(r.Identifier, r.LandingRateSeconds)).ToArray())
    {
    }

    public RunwayModeViewModel(RunwayModeDto runwayModeDto)
        : this(runwayModeDto.Identifier, runwayModeDto.AcceptanceRates.Select(r => new RunwayViewModel(r.Key, r.Value)).ToArray())
    {
    }

    public RunwayModeViewModel(RunwayModeViewModel runwayModeViewModel)
        : this(runwayModeViewModel.Identifier, runwayModeViewModel.Runways.Select(r => new RunwayViewModel(r.Identifier, r.LandingRateSeconds)).ToArray())
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

    public RunwayViewModel(string identifier, int landingRateSeconds)
    {
        Identifier = identifier;
        LandingRateSeconds = landingRateSeconds;
    }

    public string Identifier { get; }
}
