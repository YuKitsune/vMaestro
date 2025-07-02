namespace Maestro.Core.Configuration;

public class RunwayConfiguration
{
    public required string Identifier { get; init; }
    public required int LandingRateSeconds { get; init; }
}
