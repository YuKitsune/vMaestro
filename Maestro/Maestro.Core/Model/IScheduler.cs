using System.Diagnostics;
using Maestro.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Model;

public interface IScheduler
{
    void Schedule(Sequence sequence, Flight flight);
}

public class Scheduler(IPerformanceLookup performanceLookup, ILogger<Scheduler> logger) : IScheduler
{
    public void Schedule(Sequence sequence, Flight flight)
    {
        ScheduleInternal(sequence, flight, force: false);
    }

    void ScheduleInternal(Sequence sequence, Flight flight, bool force)
    {
        // TODO: Runway and terminal trajectories
        // TODO: Optimisation
        
        // Do not apply any more processing to superstable or frozen flights
        if (!force && !CanSchedule(flight))
        {
            logger.LogDebug("{Callsign} is {State}. No processing required.", flight.Callsign, flight.State);
            return;
        }
        
        var currentFlightIndex = Array.FindIndex(sequence.Flights, f => f.Callsign == flight.Callsign);
        
        // TODO: Account for runway mode changes
        var runwayMode = sequence.CurrentRunwayMode;
        var landingRate = GetLandingRate(flight, runwayMode);
        
        ComputeLandingTime(sequence, flight, currentFlightIndex, landingRate);
        
        var trailingFlight = sequence.Flights
            .Skip(currentFlightIndex + 1)
            .FirstOrDefault(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier);
        if (trailingFlight is null)
        {
            // Nobody behind us, nothing to do
            return;
        }
        
        // If the flight behind us is too close, we need to re-calculate it
        // TODO: Add a test to see if we can reduce the delay if a preceding flight moves or disappears
        var timeToTrailer = trailingFlight.ScheduledLandingTime - flight.ScheduledLandingTime;
        if (timeToTrailer < TimeSpan.Zero)
        {
            // The flight that _was_ behind us in the sequence is now in front of us
            // TODO: Can we ignore them?
            return;
        }
        
        if (timeToTrailer < landingRate)
        {
            ScheduleInternal(sequence, trailingFlight, force: true);
        }
    }

    bool CanSchedule(Flight flight)
    {
        // If it hasn't been scheduled before, then we need to schedule it
        if (!flight.HasBeenScheduled)
            return true;
        
        // Do not schedule stable, superstable, or frozen flights
        return !flight.PositionIsFixed && flight.State != State.Stable;
    }

    TimeSpan GetLandingRate(Flight flight, RunwayModeConfiguration runwayMode)
    {
        var runwayConfiguration = runwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier);
        if (runwayConfiguration is not null)
            return TimeSpan.FromSeconds(runwayConfiguration.DefaultLandingRateSeconds);
        
        // TODO: Configurable default
        return TimeSpan.FromSeconds(60);
    }

    void ComputeLandingTime(Sequence sequence, Flight flight, int currentFlightIndex, TimeSpan landingRate)
    {
        logger.LogInformation("Computing {Callsign}...", flight.Callsign);
        
        BlockoutPeriod? blockoutPeriod = null;
        if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
            blockoutPeriod = sequence.BlockoutPeriods.FirstOrDefault(bp => bp.RunwayIdentifier == flight.AssignedRunwayIdentifier && bp.StartTime < flight.EstimatedLandingTime && bp.EndTime > flight.EstimatedLandingTime);;

        DateTimeOffset? earliestAvailableLandingTime = null;
        if (blockoutPeriod is not null)
            earliestAvailableLandingTime = blockoutPeriod.EndTime;

        if (blockoutPeriod is not null)
        {
            logger.LogDebug("Blockout period exists, earliest landing time for {Callsign} is {Time}", flight.Callsign, earliestAvailableLandingTime);
        }
        
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
        var lastSuperStableFlight = sequence.Flights.LastOrDefault(f =>
            f.Callsign != flight.Callsign &&
            f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier &&
            f.PositionIsFixed);
        if (lastSuperStableFlight is not null)
        {
            var slotAfterLeader = lastSuperStableFlight.ScheduledLandingTime.Add(landingRate);
            earliestAvailableLandingTime = earliestAvailableLandingTime is null
                ? slotAfterLeader
                : DateTimeOffsetHelpers.Latest(earliestAvailableLandingTime.Value, slotAfterLeader);
            
            logger.LogDebug("Last SuperStable flight is {LeaderCallsign} at {Time}. Earliest slot time is now {Earliest}", lastSuperStableFlight.Callsign, slotAfterLeader, earliestAvailableLandingTime);
        }
        
        var scheduledLandingTime = earliestAvailableLandingTime ?? flight.EstimatedLandingTime;
            
        logger.LogDebug("Earliest available landing time for {Callsign} is {Time}. Current estimate is {Estimate}.", flight.Callsign, scheduledLandingTime, flight.EstimatedLandingTime);
        
        // Ensure sufficient spacing between the current flight and the one in front
        var leader = sequence.Flights
            .Take(currentFlightIndex)
            .LastOrDefault(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier);
        if (leader is not null)
        {
            var timeToLeader = scheduledLandingTime - leader.ScheduledLandingTime;
            if (timeToLeader < landingRate)
            {
                scheduledLandingTime = leader.ScheduledLandingTime.Add(landingRate);
                
                logger.LogInformation(
                    "Delaying {Callsign} to {NewLandingTime} for spacing with {LeaderCallsign} landing at {LeaderLandingTime}.",
                    flight.Callsign,
                    scheduledLandingTime,
                    leader.Callsign,
                    leader.ScheduledLandingTime);
            }
        }
        
        if (flight.EstimatedFeederFixTime is not null)
        {
            var delay = scheduledLandingTime - flight.EstimatedLandingTime;
            var feederFixTime = flight.EstimatedFeederFixTime.Value + delay;
            flight.SetFeederFixTime(feederFixTime);
        }
        
        flight.SetLandingTime(scheduledLandingTime);
        
        var performance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        if (performance is not null && performance.IsJet && flight.EstimatedLandingTime < scheduledLandingTime)
        {
            flight.SetFlowControls(FlowControls.S250);
        }
        else
        {
            flight.SetFlowControls(FlowControls.ProfileSpeed);
        }

        logger.LogInformation(
            "{Callsign} scheduled landing time now {NewLandingTime}. Total delay {Delay}.",
            flight.Callsign,
            scheduledLandingTime,
            flight.ScheduledLandingTime - flight.InitialLandingTime);
        
        if (flight.EstimatedLandingTime > scheduledLandingTime)
        {
            logger.LogWarning("Flight was scheduled to land at {ScheduledLandingTime} earlier than the estimated landing time of {EstimatedLandingTime}", scheduledLandingTime, flight.EstimatedLandingTime);
        }
    }
}

[DebuggerDisplay("{StartTime} - {EndTime}")]
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
        return left < right;
    }

    public static bool IsAfter(this DateTimeOffset left, DateTimeOffset right)
    {
        return left > right;
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