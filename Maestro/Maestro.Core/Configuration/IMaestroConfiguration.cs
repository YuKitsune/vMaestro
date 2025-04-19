using Microsoft.Extensions.Configuration;

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

public interface IMaestroConfiguration
{
    Uri ServerUri { get; }
    FeederFixEstimateSource FeederFixEstimateSource { get; }
    LandingEstimateSource LandingEstimateSource { get; }
}

public class MaestroConfiguration(IConfigurationSection configurationSection) : IMaestroConfiguration
{
    public Uri ServerUri => configurationSection.GetValue<Uri>("ServerUri")!;
    public FeederFixEstimateSource FeederFixEstimateSource => configurationSection.GetValue<FeederFixEstimateSource>("FeederFixEstimateSource");
    public LandingEstimateSource LandingEstimateSource => configurationSection.GetValue<LandingEstimateSource>("LandingEstimateSource");
}
