namespace Maestro.Core.Configuration;

public class TrajectoryConfiguration
{
    // Lookup parameters
    public required string FeederFix { get; init; }
    public string TransitionFix { get; init; } = string.Empty;
    public string ApproachType { get; init; } = string.Empty;
    public required string RunwayIdentifier { get; init; }

    // Route geometry — segments ordered from feeder fix to runway threshold.
    // Track and distance are pre-computed offline (e.g. via the Maestro.Tools CLI).
    public required TrajectorySegmentConfiguration[] Segments { get; init; }

    // Pressure segments describe an extended flight path ATC may use to absorb delay.
    // ETI for these segments contributes to P (Pressure window).
    public TrajectorySegmentConfiguration[] PressureSegments { get; init; } = [];

    // Max pressure segments contribute to Pmax (Maximum Pressure window).
    public TrajectorySegmentConfiguration[] MaxPressureSegments { get; init; } = [];
}
