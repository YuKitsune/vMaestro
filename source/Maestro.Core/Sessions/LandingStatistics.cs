using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Serilog;

namespace Maestro.Core.Sessions;

public interface IAchievedRate;
public record NoDeviation : IAchievedRate;
public record AchievedRate(
    TimeSpan AverageLandingInterval,
    TimeSpan LandingIntervalDeviation)
    : IAchievedRate;

public class LandingStatistics(ILogger logger)
{
    readonly ILogger _logger = logger.ForContext<LandingStatistics>();

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

        _logger.Debug("RWY {Runway}: recorded landing at {LandingTime:HHmm}, {Count} sample(s) in window",
            runway.Identifier, actualLandingTime, landingTimes.Count);

        AchievedLandingRates[runway.Identifier] = CalculateAchievedRate(runway, landingTimes);

        void RemoveStaleTimes(DateTimeOffset referenceTime, List<DateTimeOffset> times)
        {
            var oldestTime = referenceTime.Subtract(_averagingPeriod);
            times.RemoveAll(t => t.IsSameOrBefore(oldestTime));
        }
    }

    IAchievedRate CalculateAchievedRate(Runway runway, IReadOnlyList<DateTimeOffset> actualLandingTimes)
    {
        if (actualLandingTimes.Count == 0)
            return new NoDeviation();

        var diffs = new List<TimeSpan>();
        for (var i = 1; i < actualLandingTimes.Count; i++)
        {
            var previous = actualLandingTimes[i - 1];
            var current = actualLandingTimes[i];
            diffs.Add(current - previous);
        }

        if (diffs.Count == 0)
        {
            _logger.Debug("RWY {Runway}: only 1 sample, no rate computed", runway.Identifier);
            return new NoDeviation();
        }

        // If any gap exceeds 2x the acceptance rate, traffic is not busy enough to compute a rate
        var doubleRate = TimeSpan.FromSeconds(runway.AcceptanceRate.TotalSeconds * 2);
        var largeGap = diffs.FirstOrDefault(d => d >= doubleRate);
        if (largeGap != default)
        {
            _logger.Debug(
                "RWY {Runway}: gap of {Gap:F0}s exceeds 2x acceptance rate ({DoubleRate:F0}s), no rate computed",
                runway.Identifier, largeGap.TotalSeconds, doubleRate.TotalSeconds);
            return new NoDeviation();
        }

        var averageSeconds = diffs.Average(t => t.TotalSeconds);
        var averageInterval = TimeSpan.FromSeconds(averageSeconds);
        var deviation = runway.AcceptanceRate - averageInterval;

        _logger.Debug(
            "RWY {Runway}: {Samples} samples, avg {Average:F0}s, deviation {Deviation:+F0;-F0;0}s from {Target:F0}s target",
            runway.Identifier, actualLandingTimes.Count, averageSeconds, deviation.TotalSeconds, runway.AcceptanceRate.TotalSeconds);

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
