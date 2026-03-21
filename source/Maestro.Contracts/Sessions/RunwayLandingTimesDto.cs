using MessagePack;

namespace Maestro.Contracts.Sessions;

/// <summary>
/// Represents landing time records for a single runway.
/// </summary>
/// <param name="RunwayIdentifier">The runway identifier (e.g., "34L").</param>
/// <param name="ActualLandingTimes">Array of actual landing times recorded for this runway.</param>
/// <param name="AchievedRate">The calculated achieved landing rate for this runway.</param>
[MessagePackObject]
public record RunwayLandingTimesDto(
    [property: Key(0)] string RunwayIdentifier,
    [property: Key(1)] DateTimeOffset[] ActualLandingTimes,
    [property: Key(2)] IAchievedRateDto AchievedRate)
{
    public virtual bool Equals(RunwayLandingTimesDto? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return RunwayIdentifier == other.RunwayIdentifier &&
               ActualLandingTimes.SequenceEqual(other.ActualLandingTimes) &&
               EqualityComparer<IAchievedRateDto>.Default.Equals(AchievedRate, other.AchievedRate);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + RunwayIdentifier.GetHashCode();
            foreach (var time in ActualLandingTimes)
            {
                hash = hash * 31 + time.GetHashCode();
            }
            hash = hash * 31 + AchievedRate.GetHashCode();
            return hash;
        }
    }
}
