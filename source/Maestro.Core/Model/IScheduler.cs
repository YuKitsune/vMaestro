using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Serilog;

namespace Maestro.Core.Model;

public interface IScheduler
{
    Task Schedule(Sequence sequence, CancellationToken cancellationToken);
    void Schedule(Sequence sequence, Flight flight);
}

public class Scheduler(IPerformanceLookup performanceLookup, ILogger logger) : IScheduler
{
    public async Task Schedule(Sequence sequence, CancellationToken cancellationToken)
    {
        await sequence.Sort(cancellationToken);
        using (await sequence.Lock.AcquireAsync(cancellationToken))
        {
            var flights = sequence.SequencableFlights.ToList();
            flights.Sort();
            
            foreach (var flight in flights)
            {
                Schedule(sequence, flight);
            }
        }
    }
    
    public void Schedule(Sequence sequence, Flight flight)
    {
        ScheduleInternal(sequence, flight, force: false);
    }

    void ScheduleInternal(Sequence sequence, Flight flight, bool force)
    {
        // TODO: Runway and terminal trajectories
        
        // Do not apply any more processing to superstable or frozen flights
        if (!force && !CanSchedule(flight))
        {
            logger.Debug("{Callsign} is {State}. No processing required.", flight.Callsign, flight.State);
            return;
        }
        
        logger.Information("Scheduling {Callsign}.", flight.Callsign);
        
        var currentFlightIndex = Array.FindIndex(sequence.SequencableFlights, f => f.Callsign == flight.Callsign);
        
        // TODO: Account for runway mode changes
        var runwayMode = sequence.CurrentRunwayMode;
        var landingRate = GetLandingRate(flight, runwayMode);
        
        ComputeLandingTime(sequence, flight, currentFlightIndex, landingRate);
        
        var trailingFlight = sequence.SequencableFlights
            .Skip(currentFlightIndex + 1)
            .FirstOrDefault(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier);
        if (trailingFlight is null)
        {
            // Nobody behind us, nothing to do
            return;
        }
        
        logger.Debug(
            "Trailing flight is {Callsign} with ETA {LandingEstimate:HH:mm}.",
            trailingFlight.Callsign,
            trailingFlight.EstimatedLandingTime);
        
        // If the flight behind us is too close, we need to re-calculate it
        // TODO: Add a test to see if we can reduce the delay if a preceding flight moves or disappears
        var timeToTrailer = trailingFlight.ScheduledLandingTime - flight.ScheduledLandingTime;
        if (timeToTrailer < TimeSpan.Zero)
        {
            logger.Warning(
                "{Callsign} is behind in the sequence but their landing estimate is {Interval} ahead.",
                trailingFlight.Callsign,
                timeToTrailer.Duration().ToHoursAndMinutesString());
            
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
        BlockoutPeriod? blockoutPeriod = null;
        if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
            blockoutPeriod = sequence.BlockoutPeriods.FirstOrDefault(bp => bp.RunwayIdentifier == flight.AssignedRunwayIdentifier && bp.StartTime < flight.EstimatedLandingTime && bp.EndTime > flight.EstimatedLandingTime);;

        DateTimeOffset? earliestAvailableLandingTime = null;
        if (blockoutPeriod is not null)
            earliestAvailableLandingTime = blockoutPeriod.EndTime;

        if (blockoutPeriod is not null)
        {
            logger.Debug("Blockout period exists, earliest landing time for {Callsign} is {Time:HH:mm}", flight.Callsign, earliestAvailableLandingTime);
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
        var lastSuperStableFlight = sequence.SequencableFlights.LastOrDefault(f =>
            f.Callsign != flight.Callsign &&
            f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier &&
            f.PositionIsFixed);
        if (lastSuperStableFlight is not null)
        {
            var slotAfterLeader = lastSuperStableFlight.ScheduledLandingTime.Add(landingRate);
            earliestAvailableLandingTime = earliestAvailableLandingTime is null
                ? slotAfterLeader
                : DateTimeOffsetHelpers.Latest(earliestAvailableLandingTime.Value, slotAfterLeader);
            
            logger.Debug(
                "Last SuperStable flight is {LeaderCallsign} at {Time:HH:mm}. Earliest slot time is now {Earliest:HH:mm}",
                lastSuperStableFlight.Callsign,
                slotAfterLeader,
                earliestAvailableLandingTime);
        }
        
        var scheduledLandingTime = earliestAvailableLandingTime ?? flight.EstimatedLandingTime;
            
        logger.Debug(
            "Earliest available landing time for {Callsign} is {Time:HH:mm}. Current estimate is {Estimate:HH:mm}.",
            flight.Callsign,
            scheduledLandingTime,
            flight.EstimatedLandingTime);
        
        // Ensure sufficient spacing between the current flight and the one in front
        // BUG: I think there is a concurrency issue here. The leading flight could sometimes has an ETA later than us.
        var leader = sequence.SequencableFlights
            .Take(currentFlightIndex)
            .LastOrDefault(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier);
        if (leader is not null)
        {
            var timeToLeader = scheduledLandingTime - leader.ScheduledLandingTime;
            if (timeToLeader < landingRate)
            {
                scheduledLandingTime = leader.ScheduledLandingTime.Add(landingRate);
                
                logger.Information(
                    "Delaying {Callsign} to {NewLandingTime:HH:mm} for spacing with {LeaderCallsign} landing at {LeaderLandingTime:HH:mm}.",
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
                
            logger.Debug(
                "{Callsign} STA_FF now {ScheduledFeederFixTime:HH:mm} (Delay {Delay}).",
                flight.Callsign,
                feederFixTime,
                delay.ToHoursAndMinutesString());
        }
        
        flight.SetLandingTime(scheduledLandingTime);
        
        var performance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        if (performance is not null && performance.Type == AircraftType.Jet && flight.EstimatedLandingTime < scheduledLandingTime)
        {
            flight.SetFlowControls(FlowControls.S250);
        }
        else
        {
            flight.SetFlowControls(FlowControls.ProfileSpeed);
        }

        logger.Information(
            "{Callsign} STA now {NewLandingTime:HH:mm}. Total delay {Delay}.",
            flight.Callsign,
            scheduledLandingTime,
            (flight.ScheduledLandingTime - flight.InitialLandingTime).ToHoursAndMinutesString());
        
        if (flight.EstimatedLandingTime > scheduledLandingTime)
        {
            var diff = scheduledLandingTime - flight.EstimatedLandingTime;
            logger.Warning(
                "{Callsign} was scheduled to land {Difference} earlier than the estimated landing time of {EstimatedLandingTime:HH:mm}",
                flight.Callsign,
                diff.ToHoursAndMinutesString(),
                flight.EstimatedLandingTime);
        }
    }
}