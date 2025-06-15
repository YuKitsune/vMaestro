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

public class MaestroConfiguration : IMaestroConfiguration
{
    public required FeederFixEstimateSource FeederFixEstimateSource { get; init; }
    public required AircraftTypeReclassification[] Reclassifications { get; init; }
}