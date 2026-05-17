using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Contracts.Runway;
using Maestro.Core.Configuration;

namespace Maestro.Wpf.ViewModels;

public partial class RunwayModeViewModel : ObservableObject
{
    [ObservableProperty]
    RunwayViewModel[] _runways = [];

    public RunwayModeViewModel(RunwayModeConfiguration runwayModeConfiguration, int defaultOffModeSeparationSeconds)
        : this(
            runwayModeConfiguration.Identifier,
            runwayModeConfiguration.Runways.Select(r => new RunwayViewModel(r.Identifier, r.ApproachType, r.LandingRateSeconds, r.FeederFixes)).ToArray(),
            runwayModeConfiguration.DependencyRateSeconds,
            runwayModeConfiguration.OffModeSeparationSeconds ?? defaultOffModeSeparationSeconds)
    {
    }

    public RunwayModeViewModel(RunwayModeDto runwayModeDto)
        : this(
            runwayModeDto.Identifier,
            runwayModeDto.Runways.Select(r => new RunwayViewModel(r.Identifier, r.ApproachType, r.AcceptanceRateSeconds, r.FeederFixes)).ToArray(),
            runwayModeDto.DependencyRateSeconds,
            runwayModeDto.OffModeSeparationSeconds)
    {
    }

    public RunwayModeViewModel(RunwayModeViewModel runwayModeViewModel)
        : this(
            runwayModeViewModel.Identifier,
            runwayModeViewModel.Runways.Select(r => new RunwayViewModel(r.Identifier, r.ApproachType, r.LandingRateSeconds, r.FeederFixes)).ToArray(),
            runwayModeViewModel.DependencyRateSeconds,
            runwayModeViewModel.OffModeSeparationSeconds)
    {
    }

    public RunwayModeViewModel(string identifier, RunwayViewModel[] runways, int dependencyRateSeconds, int offModeSeparationSeconds)
    {
        Identifier = identifier;
        Runways = runways;
        DependencyRateSeconds = dependencyRateSeconds;
        OffModeSeparationSeconds = offModeSeparationSeconds;
    }

    public string Identifier { get; }
    public int DependencyRateSeconds { get; }
    public int OffModeSeparationSeconds { get; }
}

public partial class RunwayViewModel : ObservableObject
{
    [ObservableProperty]
    int _landingRateSeconds;

    public RunwayViewModel(string identifier, string approachType, int landingRateSeconds, string[] feederFixes)
    {
        Identifier = identifier;
        ApproachType = approachType;
        LandingRateSeconds = landingRateSeconds;
        FeederFixes = feederFixes;
    }

    public string Identifier { get; }
    public string ApproachType { get; }
    public string[] FeederFixes { get; }
}

public class RunwayIntervalViewModel
{
    public string Identifier { get; }
    public string LandingRateDisplay { get; }

    public RunwayIntervalViewModel(string identifier, string landingRateDisplay)
    {
        Identifier = identifier;
        LandingRateDisplay = landingRateDisplay;
    }
}

public class RunwayAchievedRateViewModel
{
    public string Identifier { get; }
    public string AchievedRateDisplay { get; }
    public string DeviationDisplay { get; }

    public RunwayAchievedRateViewModel(string identifier, string achievedRateDisplay, string deviationDisplay)
    {
        Identifier = identifier;
        AchievedRateDisplay = achievedRateDisplay;
        DeviationDisplay = deviationDisplay;
    }
}
