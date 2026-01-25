namespace Maestro.Core.Configuration;

public class RunwayModeConfiguration
{
    public required string Identifier { get; init; }
    public TimeSpan DependencyRate { get; init; } = TimeSpan.Zero;
    public required RunwayConfiguration[] Runways { get; init; }

    // TODO: Runway Allocation Rules
}
