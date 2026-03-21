using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;

namespace Maestro.Core.Sessions;

public interface IAchievedRate;
public record NoDeviation : IAchievedRate;
public record AchievedRate(
    TimeSpan AverageLandingInterval,
    TimeSpan LandingIntervalDeviation)
    : IAchievedRate;

public class LandingStatistics
{
    readonly TimeSpan _averagingPeriod = TimeSpan.FromHours(1); // TODO: Add to configuration

    readonly Dictionary<string, List<DateTimeOffset>> _actualLandingTimesPerRunway = new();

    public Dictionary<string, IAchievedRate> AchievedLandingRates { get; } = new();

    public void RecordLandingTime(Runway runway, DateTimeOffset actualLandingTime, IClock clock)
    {
        if (_actualLandingTimesPerRunway.TryGetValue(runway.Identifier, out var landingTimes))
        {
            landingTimes.Add(actualLandingTime);
            RemoveStaleTimes(clock.UtcNow(), _actualLandingTimesPerRunway[runway.Identifier]);
        }
        else
        {
            landingTimes = _actualLandingTimesPerRunway[runway.Identifier] = [actualLandingTime];
        }

        AchievedLandingRates[runway.Identifier] = CalculateAchievedRate(runway, landingTimes);
        void RemoveStaleTimes(DateTimeOffset referenceTime, List<DateTimeOffset> times)
        {
            var oldestTime = referenceTime.Subtract(_averagingPeriod);
            times.Where(t => t.IsSameOrBefore(oldestTime))
                .ToList()
                .ForEach(t => times.Remove(t));
        }
    }

    IAchievedRate CalculateAchievedRate(Runway runway, IReadOnlyList<DateTimeOffset> actualLandingTimes)
    {
        if (actualLandingTimes.Count == 0)
        {
            // No samples, no deviation
            return new NoDeviation();
        }

        var diffs = new List<TimeSpan>();
        for (var i = 1; i < actualLandingTimes.Count; i++)
        {
            var previous = actualLandingTimes[i - 1];
            var current = actualLandingTimes[i];

            var diff = current - previous;
            diffs.Add(diff);
        }

        // If any two flights are separated by more than 2x the desired landing rate, then it's not busy enough
        var doubleRate = TimeSpan.FromSeconds(runway.AcceptanceRate.TotalSeconds * 2);
        if (diffs.Count == 0 || diffs.Any(d => d >= doubleRate))
        {
            return new NoDeviation();
        }

        var averageSeconds = diffs.Average(t => t.TotalSeconds);
        var averageInterval = TimeSpan.FromSeconds(averageSeconds);
        var deviation = runway.AcceptanceRate - averageInterval;

        return new AchievedRate(averageInterval, deviation);
    }

    public LandingStatisticsDto Snapshot()
    {
        return new LandingStatisticsDto
        {
            RunwayLandingTimes = AchievedLandingRates.ToDictionary(
                x => x.Key,
                x => new RunwayLandingTimesDto(
                    RunwayIdentifier: x.Key,
                    ActualLandingTimes: _actualLandingTimesPerRunway[x.Key].ToArray(),
                    AchievedRate: x.Value switch
                    {
                        NoDeviation => new NoDeviationDto(),
                        AchievedRate rate => new AchievedRateDto(
                            rate.AverageLandingInterval,
                            rate.LandingIntervalDeviation),
                        _ => new NoDeviationDto()
                    }))
        };
    }

    public void Restore(LandingStatisticsDto dto)
    {
        _actualLandingTimesPerRunway.Clear();
        AchievedLandingRates.Clear();

        foreach (var kvp in dto.RunwayLandingTimes)
        {
            var runway = kvp.Key;
            var runwayLandingTimesDto = kvp.Value;
            _actualLandingTimesPerRunway[runway] = new List<DateTimeOffset>(runwayLandingTimesDto.ActualLandingTimes);

            AchievedLandingRates[runway] = runwayLandingTimesDto.AchievedRate switch
            {
                NoDeviationDto => new NoDeviation(),
                AchievedRateDto achievedRateDto => new AchievedRate(
                    achievedRateDto.AverageLandingInterval,
                    achievedRateDto.LandingIntervalDeviation),
                _ => new NoDeviation()
            };
        }
    }
}
