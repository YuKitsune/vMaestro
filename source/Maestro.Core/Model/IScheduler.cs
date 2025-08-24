using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Serilog;

namespace Maestro.Core.Model;

public interface IScheduler
{
    void Schedule(Sequence sequence);
}

// Test cases:
// - When scheduling an overshoot flight, between two frozen flights, it is scheduled in between them
// - When scheduling an overshoot flight, between two frozen flights, and no space is available, it is delayed until there is space
// - When scheduling an overshoot flight, in front of SuperStable flights, SuperStable flights are delayed
// - When scheduling an overshoot flight, the sequence is recalculated for all subsequent flights
// - When scheduling a new flight, stable flights are recalculated

public class Scheduler(
    IRunwayAssigner runwayAssigner,
    IAirportConfigurationProvider airportConfigurationProvider,
    IPerformanceLookup performanceLookup,
    IClock clock,
    ILogger logger)
    : IScheduler
{
    public void Schedule(Sequence sequence)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .Single(c => c.Identifier == sequence.AirportIdentifier);

        var sequencedFlights = new SortedSet<Flight>(FlightComparer.Instance);

        // Landed and frozen flights cannot move at all, no action required
        foreach (var flight in sequence.Flights.Where(f => f.State is State.Frozen or State.Landed))
        {
            sequencedFlights.Add(flight);
        }

        // High-priority flights
        foreach (var flight in sequence.Flights.OrderBy(f => f.ScheduledLandingTime).Where(f => f.HighPriority || f.ManualLandingTime || f.NoDelay))
        {
            sequencedFlights.Add(flight);
        }

        // Inserted flights go between or after Frozen flights, but cannot displace them
        var recalculate = false;
        foreach (var flight in sequence.Flights.Where(f => f.State is State.Overshoot))
        {
            if (HasBeenScheduled(flight))
                continue;

            if (flight.TargetLandingTime is null)
            {
                logger.Warning("Overshoot flight {Callsign} has no target landing time", flight.Callsign);
                break;
            }

            ScheduleInternal(flight, flight.TargetLandingTime.Value, flight.TargetLandingTime.Value);
            flight.SetState(State.Frozen, clock); // TODO: This state change should be configurable
            sequencedFlights.Add(flight);
            recalculate = true;
        }

        // SuperStable flights generally cannot move, though inserted flights can displace them
        foreach (var flight in sequence.Flights.OrderBy(f => f.ScheduledLandingTime).Where(f => f.State is State.SuperStable))
        {
            if (HasBeenScheduled(flight))
                continue;

            if (recalculate)
                ScheduleInternal(flight, flight.ScheduledLandingTime, flight.ScheduledLandingTime);

            sequencedFlights.Add(flight);
        }

        // New flights can displace Stable flights, but not SuperStable flights
        foreach (var flight in sequence.Flights.OrderBy(f => f.EstimatedLandingTime).Where(f => f.State is State.New))
        {
            if (HasBeenScheduled(flight))
                continue;

            var lastSuperStableFlight = sequencedFlights.LastOrDefault(f => f.State is State.SuperStable);

            ScheduleInternal(flight, lastSuperStableFlight?.ScheduledLandingTime ?? flight.EstimatedLandingTime, flight.EstimatedLandingTime);
            flight.SetState(State.Unstable, clock);

            sequencedFlights.Add(flight);
            recalculate = true;
        }

        // Stable flights can only be moved if a new flight is inserted in front of them
        foreach (var flight in sequence.Flights.OrderBy(f => f.ScheduledLandingTime).Where(f => f.State is State.Stable))
        {
            if (HasBeenScheduled(flight))
                continue;

            if (recalculate)
                ScheduleInternal(flight, flight.ScheduledLandingTime, flight.ScheduledLandingTime);

            sequencedFlights.Add(flight);
        }

        // Unstable flights are rescheduled every time
        foreach (var flight in sequence.Flights.Where(f => f.State is State.Unstable))
        {
            if (HasBeenScheduled(flight))
                continue;

            ScheduleInternal(flight, flight.EstimatedLandingTime, flight.EstimatedLandingTime);
            sequencedFlights.Add(flight);
        }

        // Set state for all sequenced flights
        foreach (var flight in sequencedFlights)
        {
            SetState(flight);
        }

        bool HasBeenScheduled(Flight flight)
        {
            return sequencedFlights.Any(f => f.Callsign == flight.Callsign);
        }

        void ScheduleInternal(Flight flight, DateTimeOffset absoluteEarliestLandingTime, DateTimeOffset targetTime)
        {
            logger.Debug("Scheduling {Callsign}", flight.Callsign);

            var currentRunwayMode = sequence.CurrentRunwayMode;
            var preferredRunways = flight.RunwayManuallyAssigned && !string.IsNullOrEmpty(flight.AssignedRunwayIdentifier)
                ? [flight.AssignedRunwayIdentifier!] // Use only the manually assigned runway
                : runwayAssigner.FindBestRunways(
                    flight.AircraftType,
                    flight.FeederFixIdentifier ?? string.Empty,
                    airportConfiguration.RunwayAssignmentRules);

tryAgain:
            DateTimeOffset? proposedLandingTime = null;
            var proposedRunway = preferredRunways.First();
            foreach (var runwayIdentifier in preferredRunways)
            {
                var runwayConfiguration = currentRunwayMode.Runways
                    .SingleOrDefault(r => r.Identifier == runwayIdentifier);
                if (runwayConfiguration is null)
                {
                    // Runway not in mode
                    continue;
                }

                var earliestLandingTimeForRunway = targetTime.IsBefore(absoluteEarliestLandingTime)
                    ? absoluteEarliestLandingTime
                    : targetTime;

                var earliestLandingTime = GetEarliestLandingTimeForRunway(earliestLandingTimeForRunway, runwayConfiguration);

                // Check if this runway has slot conflicts
                var conflictingSlot = sequence.Slots.FirstOrDefault(s =>
                    s.RunwayIdentifiers.Contains(runwayIdentifier) && s.StartTime.IsBefore(earliestLandingTime) && s.EndTime.IsAfter(earliestLandingTime));
                if (conflictingSlot is not null)
                {
                    earliestLandingTime = GetEarliestLandingTimeForRunway(conflictingSlot.EndTime, runwayConfiguration);
                }

                // If this runway results in less delay, then use that one
                if (proposedLandingTime is null || earliestLandingTime.IsBefore(proposedLandingTime.Value))
                {
                    proposedLandingTime = earliestLandingTime;
                    proposedRunway = runwayConfiguration.Identifier;
                }
            }

            if (proposedLandingTime is null)
            {
                logger.Warning("Could not schedule {Callsign}", flight.Callsign);
                return;
            }

            // If proposed landing time is after the last allowed time for current mode,
            // or if it falls in the gap between modes, delay to the next mode's first allowed time
            if (sequence.NextRunwayMode is not null && currentRunwayMode != sequence.NextRunwayMode)
            {
                var isInGap = proposedLandingTime.Value.IsAfter(sequence.LastLandingTimeForCurrentMode) &&
                                         proposedLandingTime.Value.IsBefore(sequence.FirstLandingTimeForNextMode);
                var isInNextMode = proposedLandingTime.Value.IsAfter(sequence.FirstLandingTimeForNextMode);

                if (isInGap || isInNextMode)
                {
                    logger.Debug("Flight {Callsign} delayed beyond runway mode change or into gap period, moving to next mode", flight.Callsign);

                    currentRunwayMode = sequence.NextRunwayMode;
                    absoluteEarliestLandingTime = sequence.FirstLandingTimeForNextMode;

                    goto tryAgain;
                }
            }

            // TODO: Double check how this is supposed to work
            var performance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
            if (performance is not null && performance.AircraftCategory == AircraftCategory.Jet && proposedLandingTime.Value.IsAfter(flight.EstimatedLandingTime))
            {
                flight.SetFlowControls(FlowControls.ReduceSpeed);
            }
            else
            {
                flight.SetFlowControls(FlowControls.ProfileSpeed);
            }

            Schedule(flight, proposedLandingTime.Value, proposedRunway);
        }

        DateTimeOffset GetEarliestLandingTimeForRunway(DateTimeOffset startTime, RunwayConfiguration runwayConfiguration)
        {
            var proposedLandingTime = startTime;

            // Look for conflicting flights and ensure we are not in conflict with anyone already sequenced
            // Re-run checks until no more delays are needed
            var conflictResolutionAttempts = 0;
            const int maxConflictResolutionAttempts = 100;
            bool delayApplied;

            do
            {
                delayApplied = false;
                conflictResolutionAttempts++;

                if (conflictResolutionAttempts > maxConflictResolutionAttempts)
                {
                    throw new MaestroException("Exceeded max conflict resolution attempts");
                }

                // Check leader conflict
                var leader = sequencedFlights.LastOrDefault(f => f.AssignedRunwayIdentifier == runwayConfiguration.Identifier && f.ScheduledLandingTime <= proposedLandingTime);
                if (leader is not null)
                {
                    var nextLandingTimeAfterLeader = leader.ScheduledLandingTime.AddSeconds(runwayConfiguration.LandingRateSeconds);
                    if (nextLandingTimeAfterLeader.IsAfter(proposedLandingTime))
                    {
                        proposedLandingTime = nextLandingTimeAfterLeader;
                        delayApplied = true;
                    }
                }

                // Check trailer conflict
                var trailer = sequencedFlights.FirstOrDefault(f => f.AssignedRunwayIdentifier == runwayConfiguration.Identifier && f.ScheduledLandingTime >= proposedLandingTime);
                if (trailer is not null)
                {
                    var lastLandingTimeBeforeTrailer = trailer.ScheduledLandingTime.AddSeconds(runwayConfiguration.LandingRateSeconds * -1);
                    if (lastLandingTimeBeforeTrailer.IsBefore(proposedLandingTime))
                    {
                        proposedLandingTime = trailer.ScheduledLandingTime.AddSeconds(runwayConfiguration.LandingRateSeconds);
                        delayApplied = true;
                    }
                }
            } while (delayApplied);

            return proposedLandingTime;
        }
    }

    void Schedule(Flight flight, DateTimeOffset landingTime, string runwayIdentifier)
    {
        flight.SetLandingTime(landingTime);
        flight.SetRunway(runwayIdentifier, manual: flight.RunwayManuallyAssigned);

        if (flight.EstimatedFeederFixTime is not null && !flight.HasPassedFeederFix)
        {
            var totalDelay = landingTime - flight.EstimatedLandingTime;
            var feederFixTime = flight.EstimatedFeederFixTime.Value + totalDelay;
            flight.SetFeederFixTime(feederFixTime);
        }
    }

    void SetState(Flight flight)
    {
        // TODO: Make configurable
        var stableThreshold = TimeSpan.FromMinutes(25);
        var frozenThreshold = TimeSpan.FromMinutes(15);
        var minUnstableTime = TimeSpan.FromSeconds(180);

        var timeActive = clock.UtcNow() - flight.ActivatedTime;
        var timeToFeeder = flight.EstimatedFeederFixTime - clock.UtcNow();
        var timeToLanding = flight.EstimatedLandingTime - clock.UtcNow();

        // Keep the flight unstable until it's passed the minimum unstable time
        if (timeActive < minUnstableTime)
        {
            flight.SetState(State.Unstable, clock);
            return;
        }

        if (flight.ScheduledLandingTime.IsSameOrBefore(clock.UtcNow()))
        {
            flight.SetState(State.Landed, clock);
        }
        else if (timeToLanding <= frozenThreshold)
        {
            flight.SetState(State.Frozen, clock);
        }
        else if (flight.InitialFeederFixTime?.IsSameOrBefore(clock.UtcNow()) ?? false)
        {
            flight.SetState(State.SuperStable, clock);
        }
        else if (timeToFeeder <= stableThreshold)
        {
            flight.SetState(State.Stable, clock);
        }
        else
        {
            // No change required
            return;
        }

        logger.Information("{Callsign} is now {State}", flight.Callsign, flight.State);
    }
}
