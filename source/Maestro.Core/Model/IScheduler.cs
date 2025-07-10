using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Serilog;

namespace Maestro.Core.Model;

// Crazy idea: Instead of separating each flight by absolute times, generate slots based on the landing rate
// and allocate each flight a slot based on it's estimated landing time.
// The time between slots is the landing rate, and changes dynamically based on the runway mode.
// Absolute time calculations are no longer necessary, as each slot is already spaced out appropriately.

public interface IScheduler
{
    void Schedule(Sequence sequence, Flight flight);
}

public class Scheduler(IPerformanceLookup performanceLookup, ILogger logger) : IScheduler
{
    public void Schedule(Sequence sequence, Flight flight)
    {
        ScheduleInternal(sequence, flight, force: false);
    }

    void ScheduleInternal(Sequence sequence, Flight flight, bool force)
    {
        // TODO: Runway and terminal trajectories

        // BUG: Recompute is not respected here.

        // Do not apply any more processing to superstable or frozen flights
        if (!force && !CanSchedule(flight))
        {
            logger.Debug("{Callsign} is {State}. No processing required.", flight.Callsign, flight.State);
            return;
        }

        if (flight.NoDelay)
        {
            logger.Debug("{Callsign} is a no-delay flight. No scheduling required.", flight.Callsign);
            return;
        }

        if (flight.ManualLandingTime)
        {
            logger.Debug("{Callsign} has a manual landing time. No scheduling required.", flight.Callsign);
            return;
        }

        logger.Information("Scheduling {Callsign}.", flight.Callsign);

        var currentFlightIndex = Array.FindIndex(sequence.SequencableFlights, f => f.Callsign == flight.Callsign);

        var runwayMode = sequence.GetRunwayModeAt(flight.EstimatedLandingTime);
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

    TimeSpan GetLandingRate(Flight flight, RunwayMode runwayMode)
    {
        var runwayConfiguration = runwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier);
        if (runwayConfiguration is not null)
            return TimeSpan.FromSeconds(runwayConfiguration.LandingRateSeconds);

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

        // Do not schedule flights to land earlier than their estimate
        var scheduledLandingTime = earliestAvailableLandingTime is not null
            ? DateTimeOffsetHelpers.Latest(earliestAvailableLandingTime.Value, flight.EstimatedLandingTime)
            : flight.EstimatedLandingTime;

        // Ensure sufficient spacing between the current flight and the one in front
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

        // Ensure sufficient spacing with all fixed flights behind
        bool conflict;
        do
        {
            conflict = false;
            var fixedFlightsBehind = sequence.SequencableFlights
                .Skip(currentFlightIndex + 1)
                .Where(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier && (f.NoDelay || f.ManualLandingTime))
                .OrderBy(f => f.ScheduledLandingTime)
                .ToList();

            foreach (var fixedFlight in fixedFlightsBehind)
            {
                var minAllowedTime = fixedFlight.ScheduledLandingTime.Add(landingRate.Negate());
                var maxAllowedTime = fixedFlight.ScheduledLandingTime.Add(landingRate);
                if (scheduledLandingTime.IsAfter(minAllowedTime) && scheduledLandingTime.IsBefore(maxAllowedTime))
                {
                    logger.Information(
                        "Delaying {Callsign} to {NewLandingTime:HH:mm} to avoid conflict with {ConflictingCallsign} (NoDelay/ManualLandingTime) at {ConflictingLandingTime:HH:mm}.",
                        flight.Callsign,
                        minAllowedTime,
                        fixedFlight.Callsign,
                        fixedFlight.ScheduledLandingTime);
                    scheduledLandingTime = fixedFlight.ScheduledLandingTime.Add(landingRate);
                    conflict = true;
                    break; // Re-check all fixed flights after updating
                }
            }
        } while (conflict);

        if (flight.EstimatedFeederFixTime is not null && !flight.HasPassedFeederFix)
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

        // BUG: Need to check if this flight is pushed back into a blockout period.
        //  If so, re-calculate based on blockout period.
        //  Can this be done first? (Probe the leader, check the time between leader STA and blockout)
        flight.SetLandingTime(scheduledLandingTime);

        var performance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        if (performance is not null && performance.AircraftCategory == AircraftCategory.Jet && flight.EstimatedLandingTime < scheduledLandingTime)
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
