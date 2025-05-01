using Microsoft.Extensions.Configuration;

namespace Maestro.Core.Configuration;

public enum FeederFixEstimateSource
{
    SystemEstimate,
    Trajectory
}

public interface IMaestroConfiguration
{
    Uri ServerUri { get; }
    FeederFixEstimateSource FeederFixEstimateSource { get; }
}

public class MaestroConfiguration(IConfigurationSection configurationSection) : IMaestroConfiguration
{
    public Uri ServerUri => configurationSection.GetValue<Uri>("ServerUri")!;
    public FeederFixEstimateSource FeederFixEstimateSource => configurationSection.GetValue<FeederFixEstimateSource>("FeederFixEstimateSource");
}
