namespace Maestro.Core.Configuration;

public enum FeederFixEstimateSource
{
    SystemEstimate,
    Trajectory
}

public enum LandingEstimateSource
{
    SystemEstimate,
    PresetInterval
}

public interface IEstimateConfiguration
{
    FeederFixEstimateSource FeederFixEstimateSource();
    LandingEstimateSource LandingEstimateSource();
}