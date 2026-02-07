namespace Maestro.Core.Configuration;

public class RunwayConfiguration
{
    public required string Identifier { get; init; }
    public string ApproachType { get; init; } = string.Empty;
    public required int LandingRateSeconds { get; init; }

    public string[] FeederFixes { get; init; } = [];
}
