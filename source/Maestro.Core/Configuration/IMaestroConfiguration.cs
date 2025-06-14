using Microsoft.Extensions.Configuration;

namespace Maestro.Core.Configuration;

public enum FeederFixEstimateSource
{
    SystemEstimate,
    Trajectory
}

public interface IMaestroConfiguration
{
    FeederFixEstimateSource FeederFixEstimateSource { get; }
    AircraftTypeReclassification[] Reclassifications { get; }
    
}

public class MaestroConfiguration(IConfigurationSection configurationSection) : IMaestroConfiguration
{
    public FeederFixEstimateSource FeederFixEstimateSource => configurationSection.GetValue<FeederFixEstimateSource>("FeederFixEstimateSource");
    public AircraftTypeReclassification[] Reclassifications => configurationSection.GetValue<AircraftTypeReclassification[]>("Reclassifications") ?? [];
}