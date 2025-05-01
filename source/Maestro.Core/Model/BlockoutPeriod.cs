using System.Diagnostics;

namespace Maestro.Core.Model;

[DebuggerDisplay("{RunwayIdentifier}: {StartTime} - {EndTime}")]
public class BlockoutPeriod
{
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required string RunwayIdentifier { get; init; }
}