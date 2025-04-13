namespace Maestro.Core.Configuration;

public class RunwayConfiguration
{
    public required string Identifier { get; init; }
    public required int DefaultLandingRateSeconds { get; init; }
}
