using Maestro.Core.Configuration;

namespace Maestro.Core.Model;

public interface IScheduler
{
    IEnumerable<Flight> ScheduleFlights(
        IEnumerable<Flight> flights,
        BlockoutPeriod[] blockoutPeriods,
        RunwayModeConfiguration currentRunwayMode);
}

public class Scheduler : IScheduler
{
    readonly ISeparationRuleProvider _separationRuleProvider;

    public Scheduler(ISeparationRuleProvider separationRuleProvider)
    {
        _separationRuleProvider = separationRuleProvider;
    }

    public IEnumerable<Flight> ScheduleFlights(
        IEnumerable<Flight> flights,
        BlockoutPeriod[] blockoutPeriods,
        RunwayModeConfiguration currentRunwayMode)
    {
        // BUG: If a flight on the ground has bad estimates, they can mess up the sequence putting everyone behind them
        
        var orderedFlights = flights.OrderBy(f => f.ScheduledLandingTime).ToList();
        
        var sequence = orderedFlights.Where(flight => flight.PositionIsFixed).ToList();

        foreach (var flight in orderedFlights)
        {
            // TODO: Runway and terminal trajectories
            // TODO: Optimisation
            
            if (flight.PositionIsFixed)
                continue;
            
            BlockoutPeriod? blockoutPeriod = null;
            if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
                blockoutPeriod = blockoutPeriods.FirstOrDefault(bp => bp.RunwayIdentifier == flight.AssignedRunwayIdentifier && bp.StartTime < flight.EstimatedLandingTime && bp.EndTime > flight.EstimatedLandingTime);;

            DateTimeOffset? earliestAvailableLandingTime = null;
            if (blockoutPeriod is not null)
                earliestAvailableLandingTime = blockoutPeriod.EndTime;
            
            // TODO: Move conflicting flights behind this one
            // Avoid delaying priority flights behind others
            if (flight.NoDelay || flight.HighPriority)
            {
                var naturalLandingTime = flight.EstimatedLandingTime;
                var landingTime = earliestAvailableLandingTime is not null && flight.EstimatedLandingTime < earliestAvailableLandingTime
                    ? earliestAvailableLandingTime.Value
                    : flight.EstimatedLandingTime;

                if (flight.EstimatedFeederFixTime is not null)
                {
                    var delay = landingTime - naturalLandingTime;
                    var feederFixTime = flight.EstimatedFeederFixTime.Value + delay;
                    flight.SetFeederFixTime(feederFixTime);
                }
                
                flight.SetLandingTime(landingTime);
                sequence.Add(flight);
                continue;
            }
            
            // TODO: Account for runway mode changes
            var runwayMode = currentRunwayMode;
            
            var leadingFlight = sequence.LastOrDefault();
            if (leadingFlight is not null)
            {
                TimeSpan? landingRate = null;
                
                // Use the landing rate for flights to the same runway
                if (leadingFlight.AssignedRunwayIdentifier is not null &&
                    leadingFlight.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier)
                {
                    var runwayConfiguration = runwayMode.Runways.Single(r => r.Identifier == flight.AssignedRunwayIdentifier);
                    landingRate = TimeSpan.FromSeconds(runwayConfiguration.DefaultLandingRateSeconds);
                }
                
                // Use the stagger rate when the flights have been assigned different runways
                else if (leadingFlight.AssignedRunwayIdentifier is not null &&
                         leadingFlight.AssignedRunwayIdentifier != flight.AssignedRunwayIdentifier)
                {
                    var staggerRate = TimeSpan.FromSeconds(runwayMode.StaggerRateSeconds);
                    landingRate = staggerRate.TotalSeconds < 30
                        ? TimeSpan.FromSeconds(30)
                        : staggerRate;
                }
                
                // Default to minimum separation
                landingRate ??= _separationRuleProvider.GetRequiredSpacing(
                    leadingFlight,
                    flight);
                
                earliestAvailableLandingTime = leadingFlight.ScheduledLandingTime + landingRate;
            }
            
            var scheduledLandingTime = earliestAvailableLandingTime ?? flight.EstimatedLandingTime;
            if (flight.EstimatedFeederFixTime is not null)
            {
                var delay = scheduledLandingTime - flight.EstimatedLandingTime;
                var feederFixTime = flight.EstimatedFeederFixTime.Value + delay;
                flight.SetFeederFixTime(feederFixTime);
            }
            
            flight.SetLandingTime(scheduledLandingTime);
            sequence.Add(flight);
        }
        
        return sequence.ToArray();
    }
}