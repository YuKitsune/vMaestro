using Maestro.Core.Configuration;

namespace Maestro.Core.Model;

public interface IScheduler
{
    IEnumerable<Flight> ScheduleFlights(
        IReadOnlyList<Flight> flights,
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
        IReadOnlyList<Flight> flights,
        BlockoutPeriod[] blockoutPeriods,
        RunwayModeConfiguration currentRunwayMode)
    {
        var sequence = flights.Where(f => !CanSchedule(f)).ToList();
        
        foreach (var flight in flights.OrderBy(f => f.ScheduledLandingTime))
        {
            // TODO: Runway and terminal trajectories
            // TODO: Optimisation
            
            // Do not apply any more processing to superstable or frozen flights
            if (!CanSchedule(flight))
                continue;
            
            // TODO: Account for runway mode changes
            var runwayMode = currentRunwayMode;
            var landingRate = GetLandingRate(flight, runwayMode);
            
            ComputeLandingTime(flight, landingRate);
            sequence.Add(flight);
            
            // If there are any flights behind this one, then we need to push them back
            var trailingConflictPeriod = new Period(flight.ScheduledLandingTime, flight.ScheduledLandingTime.Add(landingRate));
            var trailingFlights = sequence.Where(f =>
                f.Callsign != flight.Callsign &&
                f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier &&
                f.ScheduledLandingTime >= trailingConflictPeriod.StartTime &&
                f.ScheduledLandingTime < trailingConflictPeriod.EndTime);
            foreach (var trailingFlight in trailingFlights)
            {
                ComputeLandingTime(trailingFlight, landingRate);
            }
        }
        
        return sequence.ToArray();

        void ComputeLandingTime(Flight flight, TimeSpan landingRate)
        {
            BlockoutPeriod? blockoutPeriod = null;
            if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
                blockoutPeriod = blockoutPeriods.FirstOrDefault(bp => bp.RunwayIdentifier == flight.AssignedRunwayIdentifier && bp.StartTime < flight.EstimatedLandingTime && bp.EndTime > flight.EstimatedLandingTime);;

            DateTimeOffset? earliestAvailableLandingTime = null;
            if (blockoutPeriod is not null)
                earliestAvailableLandingTime = blockoutPeriod.EndTime;
            
            // Avoid delaying priority flights behind others
            // TODO: Move conflicting flights behind this one
            // if (flight.NoDelay || flight.HighPriority)
            // {
            //     var naturalLandingTime = flight.EstimatedLandingTime;
            //     var landingTime = earliestAvailableLandingTime is not null && flight.EstimatedLandingTime < earliestAvailableLandingTime
            //         ? earliestAvailableLandingTime.Value
            //         : flight.EstimatedLandingTime;
            //
            //     if (flight.EstimatedFeederFixTime is not null)
            //     {
            //         var delay = landingTime - naturalLandingTime;
            //         var feederFixTime = flight.EstimatedFeederFixTime.Value + delay;
            //         flight.SetFeederFixTime(feederFixTime);
            //     }
            //     
            //     flight.SetLandingTime(landingTime);
            //     sequence.Add(flight);
            //     continue;
            // }
            
            // Flights cannot be scheduled in front of superstable flights
            var lastSuperStableFlight = sequence.LastOrDefault(f => f.Callsign != flight.Callsign && f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier && f.PositionIsFixed);
            if (lastSuperStableFlight is not null)
            {
                var slotAfterLeader = lastSuperStableFlight.ScheduledLandingTime.Add(landingRate);
                earliestAvailableLandingTime = earliestAvailableLandingTime is null
                    ? slotAfterLeader
                    : DateTimeOffsetHelpers.Latest(earliestAvailableLandingTime.Value, slotAfterLeader);
            }
            
            // New flights (not yet scheduled) can go in front of stable flights if they have an earlier estimate
            var lastStableFlight = sequence.LastOrDefault(f => f.Callsign != flight.Callsign && f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier && f.State is State.Stable);
            if (flight.HasBeenScheduled && lastStableFlight is not null)
            {
                var slotAfterLeader = lastStableFlight.ScheduledLandingTime.Add(landingRate);
                earliestAvailableLandingTime = earliestAvailableLandingTime is null
                    ? slotAfterLeader
                    : DateTimeOffsetHelpers.Latest(earliestAvailableLandingTime.Value, slotAfterLeader);
            }
            
            var scheduledLandingTime = earliestAvailableLandingTime ?? flight.EstimatedLandingTime;
            
            // Keep working backwards until there are no conflicts
            while (true)
            {
                var conflictPeriod = new Period(scheduledLandingTime.Subtract(landingRate), scheduledLandingTime);
            
                var leader = sequence
                    .OrderBy(f => f.ScheduledLandingTime)
                    .LastOrDefault(f => f.Callsign != flight.Callsign && f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier && f.ScheduledLandingTime > conflictPeriod.StartTime && f.ScheduledLandingTime <= conflictPeriod.EndTime);
                
                if (leader is null)
                    break;

                scheduledLandingTime = leader.ScheduledLandingTime.Add(landingRate);
            }
            
            if (flight.EstimatedFeederFixTime is not null)
            {
                var delay = scheduledLandingTime - flight.EstimatedLandingTime;
                var feederFixTime = flight.EstimatedFeederFixTime.Value + delay;
                flight.SetFeederFixTime(feederFixTime);
            }
            
            flight.SetLandingTime(scheduledLandingTime);
        }
    }

    TimeSpan GetLandingRate(Flight flight, RunwayModeConfiguration runwayMode)
    {
        var runwayConfiguration = runwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier);
        if (runwayConfiguration is not null)
            return TimeSpan.FromSeconds(runwayConfiguration.DefaultLandingRateSeconds);
        
        // TODO: Configurable default
        return TimeSpan.FromSeconds(60);
    }

    bool CanSchedule(Flight flight)
    {
        // If it hasn't been scheduled before, then we need to schedule it
        if (!flight.HasBeenScheduled)
            return true;
        
        // Do not schedule stable, superstable, or frozen flights
        return !flight.PositionIsFixed && flight.State != State.Stable;
    }
}

public struct Period
{
    public Period(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        if (endTime < startTime)
            throw new ArgumentException("EndTime cannot be earlier than startTime");
            
        StartTime = startTime;
        EndTime = endTime;
    }
        
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }

    public bool Contains(DateTimeOffset dateTime)
    {
        return dateTime >= StartTime && dateTime <= EndTime;
    }
}

public static class DateTimeOffsetExtensionMethods
{
    public static bool IsBefore(this DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right;
    }

    public static bool IsAfter(this DateTimeOffset left, DateTimeOffset right)
    {
        return left >= right;
    }

    public static bool IsWithin(this DateTimeOffset left, TimeSpan tolerance, DateTimeOffset referencePoint)
    {
        var start = tolerance.Ticks >= 0
            ? referencePoint.Subtract(tolerance)
            : referencePoint.Add(tolerance);
        
        var end = tolerance.Ticks >= 0
            ? referencePoint.Add(tolerance)
            : referencePoint.Subtract(tolerance);
        
        var period = new Period(start, end);
        return period.Contains(left);
    }
}

public static class DateTimeOffsetHelpers
{
    public static DateTimeOffset Earliest(DateTimeOffset left, DateTimeOffset right)
    {
        return left.IsBefore(right) ? left : right;
    }
    
    public static DateTimeOffset Latest(DateTimeOffset left, DateTimeOffset right)
    {
        return left.IsAfter(right) ? left : right;
    }
}