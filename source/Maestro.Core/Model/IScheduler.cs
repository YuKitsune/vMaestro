using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Serilog;

namespace Maestro.Core.Model;

public interface IScheduler
{
    void Schedule(Sequence sequence);
}

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

        var sequencableFlights = sequence.Flights
            .Where(f => f.State is not State.Desequenced and not State.Removed)
            .ToList();

        // TODO: Stable and SuperStable flights should still be processed, but only if:
        // 1. A new flight is introduced in front of them (Stable flights only)
        // 2. A flight is manually inserted in front of them
        // 3. A slot has been added that conflicts with their landing time
        // Only Frozen flights should be **completely** frozen
        var sequencedFlights = new SortedSet<Flight>(FlightComparer.Instance);
        foreach (var flight in sequencableFlights.Where(f => f.State is State.Stable or State.SuperStable or State.Frozen or State.Landed))
        {
            sequencedFlights.Add(flight);
        }

        var flightsToSchedule = sequencableFlights
            .Where(f => f.State is State.New or State.Unstable)
            .OrderBy(f => f.EstimatedLandingTime)
            .ToList();

        // First pass: NoDelay
        foreach (var flight in flightsToSchedule.Where(f => f.NoDelay))
        {
            // TODO: Consider runway assignment
            Schedule(flight, flight.EstimatedLandingTime, flight.AssignedRunwayIdentifier);
            sequencedFlights.Add(flight);
        }

        // Second pass: Manual landing time
        foreach (var flight in flightsToSchedule.Where(f => f.ManualLandingTime))
        {
            // TODO: Consider runway assignment

            // Landing time is set elsewhere, so we can just add it as-is
            sequencedFlights.Add(flight);
        }

        // TODO: If any flights have ManualLandingTime or NoDelay, we need to make sure no Stable flights are in conflict with them
        // If there are, they need to be delayed to avoid conflicting with the NoDelay or ManualLandingTime flights.

        // Third pass: High priority
        foreach (var flight in flightsToSchedule.Where(f => f.HighPriority))
        {
            ScheduleInternal(flight);
            sequencedFlights.Add(flight);
        }

        // Fourth pass: Everyone else
        foreach (var flight in flightsToSchedule.Where(f => f is {NoDelay: false, ManualLandingTime:false, HighPriority: false}))
        {
            ScheduleInternal(flight);
            sequencedFlights.Add(flight);
        }

        // Set state for all sequenced flights
        foreach (var flight in sequencedFlights)
        {
            SetState(flight);
        }

        void ScheduleInternal(Flight flight)
        {
            logger.Debug("Scheduling {Callsign}", flight.Callsign);

            var currentRunwayMode = sequence.CurrentRunwayMode;
            var preferredRunways = flight.RunwayManuallyAssigned && !string.IsNullOrEmpty(flight.AssignedRunwayIdentifier)
                ? [flight.AssignedRunwayIdentifier!] // Use only the manually assigned runway
                : runwayAssigner.FindBestRunways(
                    flight.AircraftType,
                    flight.FeederFixIdentifier ?? string.Empty,
                    airportConfiguration.RunwayAssignmentRules);

            DateTimeOffset absoluteEarliestLandingTime = flight.EstimatedLandingTime;
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

                var earliestLandingTimeForRunway = flight.EstimatedLandingTime.IsBefore(absoluteEarliestLandingTime)
                    ? absoluteEarliestLandingTime
                    : flight.EstimatedLandingTime;

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
                flight.SetFlowControls(FlowControls.S250);
            }
            else
            {
                flight.SetFlowControls(FlowControls.ProfileSpeed);
            }

            Schedule(flight, proposedLandingTime.Value, proposedRunway);
        }

        // Transition New flights to Unstable after scheduling
        foreach (var flight in sequencableFlights.Where(f => f.State == State.New))
        {
            flight.SetState(State.Unstable, clock);
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
