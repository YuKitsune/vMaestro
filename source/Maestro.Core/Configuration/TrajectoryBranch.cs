namespace Maestro.Core.Configuration;

public class TrajectoryBranch
{
    public required string After { get; init; }
    public TrajectorySegmentConfiguration[] Segments { get; init; } = [];
}
