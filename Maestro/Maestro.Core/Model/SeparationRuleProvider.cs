using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Configuration;

namespace Maestro.Core.Model;

public interface ISeparationRuleProvider
{
    TimeSpan GetRequiredSpacing(Flight leader, Flight trailer, RunwayModeConfigurationDto runwayMode);
}

public class SeparationRuleProvider(
    IPerformanceLookup performanceLookup,
    ISeparationConfigurationProvider separationConfigurationProvider)
    : ISeparationRuleProvider
{
    public TimeSpan GetRequiredSpacing(Flight leader, Flight trailer, RunwayModeConfigurationDto runwayMode)
    {
        var leaderPerformance = performanceLookup.GetPerformanceDataFor(leader.AircraftType);
        var trailerPerformance = performanceLookup.GetPerformanceDataFor(trailer.AircraftType);

        if (leader.AssignedRunwayIdentifier is null || leader.AssignedRunwayIdentifier != trailer.AssignedRunwayIdentifier)
        {
            var staggerRate = runwayMode.StaggerRate;
            return staggerRate.TotalSeconds < 30
                ? TimeSpan.FromSeconds(30)
                : staggerRate;
        }

        var rate = runwayMode.LandingRates[leader.AssignedRunwayIdentifier];
        if (rate.TotalSeconds < 30)
            rate = TimeSpan.FromSeconds(30);

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

            if ((rule.WakeCategoryLeader == leaderPerformance.WakeCategory ||
                 rule.AircraftTypeCodesLeader.Contains(leader.AircraftType)) &&
                (rule.WakeCategoryFollower == trailerPerformance.WakeCategory ||
                 rule.AircraftTypeCodesFollower.Contains(trailer.AircraftType)))
                return rule.Interval;
        }

        return rate;
    }
}