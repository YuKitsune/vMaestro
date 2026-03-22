using MessagePack;

namespace Maestro.Contracts.Sessions;

/// <summary>
/// Represents landing statistics tracking actual landing times and achieved rates per runway.
/// </summary>
[MessagePackObject]
public class LandingStatisticsDto : IEquatable<LandingStatisticsDto>
{
    /// <summary>
    /// Landing time records for each runway.
    /// </summary>
    [Key(0)]
    public required Dictionary<string, RunwayLandingTimesDto> RunwayLandingTimes { get; init; }

    public bool Equals(LandingStatisticsDto? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (RunwayLandingTimes.Count != other.RunwayLandingTimes.Count)
            return false;

        foreach (var kvp in RunwayLandingTimes)
        {
            if (!other.RunwayLandingTimes.TryGetValue(kvp.Key, out var otherValue))
                return false;

            if (!kvp.Value.Equals(otherValue))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as LandingStatisticsDto);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var kvp in RunwayLandingTimes.OrderBy(x => x.Key))
            {
                hash = hash * 31 + kvp.Key.GetHashCode();
                hash = hash * 31 + kvp.Value.GetHashCode();
            }
            return hash;
        }
    }
}
