namespace Maestro.Tools;

public class ToolsConfiguration
{
    public AirportToolsConfiguration[] Airports { get; init; } = [];
}

public class AirportToolsConfiguration
{
    public required string ICAO { get; init; }
    public string[] FeederFixes { get; init; } = [];
    public required string Output { get; init; }
    public PressureConfigurationOverride[] PressureConfiguration { get; init; } = [];
}

public class PressureConfigurationOverride
{
    public required string[] FeederFixes { get; init; }
    public string? TransitionFix { get; init; }
    public required string RunwayIdentifier { get; init; }
    public string? ApproachType { get; init; }
    public BranchingTrajectory? Pressure { get; init; }
    public BranchingTrajectory? MaxPressure { get; init; }
}

public class BranchingTrajectory
{
    public required string After { get; init; }
    public required SegmentDefinition[] Segments { get; init; }
}

public class SegmentDefinition
{
    public required string Identifier { get; init; }
    public required double Track { get; init; }
    public required double DistanceNM { get; init; }
}
