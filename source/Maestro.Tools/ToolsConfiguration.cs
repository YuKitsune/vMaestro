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
    public TrajectorySegmentOverride[] AdditionalSegments { get; init; } = [];
    public TrajectorySegmentOverride[] PressureSegments { get; init; } = [];
    public TrajectorySegmentOverride[] MaxPressureSegments { get; init; } = [];
}

public class TrajectorySegmentOverride
{
    public required string FeederFix { get; init; }
    public string? TransitionFix { get; init; }
    public required string RunwayIdentifier { get; init; }
    public string? ApproachType { get; init; }
    public SegmentDefinition[] Segments { get; init; } = [];
}

public class SegmentDefinition
{
    public required string Identifier { get; init; }
    public required double Track { get; init; }
    public required double DistanceNM { get; init; }
}
