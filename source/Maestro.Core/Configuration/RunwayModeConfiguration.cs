namespace Maestro.Core.Configuration;

public class RunwayModeConfiguration
{
    public required string Identifier { get; init; }
    public required RunwayConfiguration[] Runways { get; init; }
    public int OffModeSeparationSeconds { get; init; } = 30;
}

public class RunwayConfiguration
{
    public required string Identifier { get; init; }
    public required string ApproachType { get; init; } = string.Empty;
    public required int LandingRateSeconds { get; init; }
    public RunwayDependency[] Dependencies { get; init; } = [];
}

public class RunwayDependency
{
    public required string RunwayIdentifier { get; init; }
    public required int SeparationSeconds { get; init; }
}
