namespace Maestro.Core.Configuration;

public class RunwayConfiguration
{
    public required string Identifier { get; init; }
    public required string ApproachType { get; init; }
    public required int LandingRateSeconds { get; init; }

    public string[] FeederFixes { get; init; } = [];
}
