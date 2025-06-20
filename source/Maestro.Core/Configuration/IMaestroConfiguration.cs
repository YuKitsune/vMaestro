namespace Maestro.Core.Configuration;

public interface IMaestroConfiguration
{
    AircraftTypeReclassification[] Reclassifications { get; }
}

public class MaestroConfiguration : IMaestroConfiguration
{
    public required AircraftTypeReclassification[] Reclassifications { get; init; }
}