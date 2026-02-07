namespace Maestro.Core.Configuration;

public class RunwayModeConfiguration
{
    public required string Identifier { get; init; }
    public int DependencyRateSeconds { get; init; } = 0;
    public int OffModeSeparationSeconds { get; init; } = 0;
    public required RunwayConfiguration[] Runways { get; init; }
}
