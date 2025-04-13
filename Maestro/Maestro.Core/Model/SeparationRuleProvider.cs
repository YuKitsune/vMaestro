using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Configuration;

namespace Maestro.Core.Model;

public interface ISeparationRuleProvider
{
    TimeSpan GetRequiredSpacing(Flight leader, Flight trailer);
}

public class SeparationRuleProvider(
    IPerformanceLookup performanceLookup,
    ISeparationConfigurationProvider separationConfigurationProvider)
    : ISeparationRuleProvider
{
    public TimeSpan GetRequiredSpacing(Flight leader, Flight trailer)
    {
        var leaderPerformance = performanceLookup.GetPerformanceDataFor(leader.AircraftType);
        var trailerPerformance = performanceLookup.GetPerformanceDataFor(trailer.AircraftType);

        // TODO: Account for trailing aircraft with a higher approach speed
        // double speed = CalculateGroundSpeedAtPosition(trailer.FDR, airport.Position);

        foreach (var rule in separationConfigurationProvider.GetSeparationRules())
        {
            // TODO: Account for trailing aircraft with a higher approach speed
            // var minInterval = rule.Interval;
            // if (rule.Distance != 0)
            // {
            //     var distTime = TimeSpan.FromHours(rule.Distance / speed);
            //     if (distTime > rule.Interval)
            //         minInterval = distTime;
            // }
            // if (minInterval < rate)
            //     continue;

            if ((rule.WakeCategoryLeader == leaderPerformance?.WakeCategory ||
                 rule.AircraftTypeCodesLeader.Contains(leader.AircraftType)) &&
                (rule.WakeCategoryFollower == trailerPerformance?.WakeCategory ||
                 rule.AircraftTypeCodesFollower.Contains(trailer.AircraftType)))
                return rule.Interval;
        }

        // TODO: Make this configurable
        // Default to 2 minutes
        return TimeSpan.FromMinutes(2);
    }
}