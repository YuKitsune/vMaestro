namespace Maestro.Core.Model;

public class BlockoutPeriod
{
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required string RunwayIdentifier { get; init; }
    public bool IsUserProvided { get; init; }
}