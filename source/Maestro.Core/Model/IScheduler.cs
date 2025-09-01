using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Serilog;

namespace Maestro.Core.Model;

public interface IScheduler
{
    void Schedule(Sequence sequence, bool recalculateAll = false);
}

public class Scheduler(
    IRunwayScoreCalculator runwayScoreCalculator,
    IAirportConfigurationProvider airportConfigurationProvider,
    IPerformanceLookup performanceLookup,
    IClock clock,
    ILogger logger)
    : IScheduler
{
    public void Schedule(Sequence sequence, bool recalculateAll = false)
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

        // Manual landing time and no-delay are sequenced as-is
        var recalculate = recalculateAll;
        foreach (var flight in sequence.Flights.OrderBy(f => f.ScheduledLandingTime).Where(f => f.ManualLandingTime || f.NoDelay))
        {
            if (HasBeenScheduled(flight))
                continue;

            if (flight.State == State.New)
            {
                recalculate = true;
                flight.SetState(State.Unstable, clock);
            }

            sequencedFlights.Add(flight);
        }

        // High-priority flights get scheduled first
        foreach (var flight in sequence.Flights.OrderBy(f => f.ScheduledLandingTime).Where(f => f.HighPriority))
        {
            if (HasBeenScheduled(flight))
                continue;

            ScheduleInternal(flight, flight.EstimatedLandingTime);
            if (flight.State == State.New)
            {
                recalculate = true;
                flight.SetState(State.Unstable, clock);
            }

            sequencedFlights.Add(flight);
        }

        // Inserted flights go between or after Frozen flights, but cannot displace them
        foreach (var flight in sequence.Flights.Where(f => f.State is State.Overshoot))
        {
            if (HasBeenScheduled(flight))
                continue;

            ScheduleInternal(flight, flight.ScheduledLandingTime);
            sequencedFlights.Add(flight);
            recalculate = true;
        }

        // SuperStable flights generally cannot move, though inserted flights can displace them
        foreach (var flight in sequence.Flights.OrderBy(f => f.ScheduledLandingTime).Where(f => f.State is State.SuperStable))
        {
            if (HasBeenScheduled(flight))
                continue;

            if (recalculate)
                ScheduleInternal(flight, flight.ScheduledLandingTime);

            sequencedFlights.Add(flight);
        }

        // New flights can displace Stable flights, but not SuperStable flights
        foreach (var flight in sequence.Flights.OrderBy(f => f.EstimatedLandingTime).Where(f => f.State is State.New))
        {
            if (HasBeenScheduled(flight))
                continue;

            var lastSuperStableFlight = sequencedFlights.LastOrDefault(f => f.State is State.SuperStable);

            ScheduleInternal(flight, lastSuperStableFlight?.ScheduledLandingTime ?? flight.EstimatedLandingTime);
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
                ScheduleInternal(flight, flight.ScheduledLandingTime);

            sequencedFlights.Add(flight);
        }

        // Unstable flights are rescheduled every time
        foreach (var flight in sequence.Flights.OrderBy(f => f.EstimatedLandingTime).Where(f => f.State is State.Unstable))
        {
            if (HasBeenScheduled(flight))
                continue;

            ScheduleInternal(flight, flight.EstimatedLandingTime);
            sequencedFlights.Add(flight);
        }

        bool HasBeenScheduled(Flight flight)
        {
            return sequencedFlights.Any(f => f.Callsign == flight.Callsign);
        }

        void ScheduleInternal(Flight flight, DateTimeOffset absoluteEarliestLandingTime)
        {
            logger.Debug("Scheduling {Callsign}", flight.Callsign);

            var currentRunwayMode = sequence.CurrentRunwayMode;

tryAgain:
            var eligibleRunways = flight.RunwayManuallyAssigned && !string.IsNullOrEmpty(flight.AssignedRunwayIdentifier)
                ? airportConfiguration.Runways.Where(r => r.Identifier == flight.AssignedRunwayIdentifier).ToArray()
                : airportConfiguration.Runways.Where(r => IsRunwayEligible(r, flight)).ToArray();

            var preferredRunways = flight.RunwayManuallyAssigned && !string.IsNullOrEmpty(flight.AssignedRunwayIdentifier)
                ? [flight.AssignedRunwayIdentifier!] // Use only the manually assigned runway
                : runwayScoreCalculator.CalculateScores(
                    eligibleRunways,
                    flight.AircraftType,
                    flight.WakeCategory,
                    flight.FeederFixIdentifier)
                    .OrderByDescending(r => r.Score)
                    .Select(r => r.RunwayIdentifier)
                    .ToArray();

            // Fallback to the first runway in the current mode
            // TODO: Need to refactor runway assignment so this isn't a problem
            if (!preferredRunways.Any())
            {
                preferredRunways = [currentRunwayMode.Runways.First().Identifier];
            }

            DateTimeOffset? proposedLandingTime = null;
            var proposedRunway = preferredRunways.FirstOrDefault();
            foreach (var runwayIdentifier in preferredRunways)
            {
                var runwayConfiguration = currentRunwayMode.Runways
                    .SingleOrDefault(r => r.Identifier == runwayIdentifier);
                if (runwayConfiguration is null)
                {
                    // Runway not in mode
                    continue;
                }

                var earliestLandingTime = GetEarliestLandingTimeForRunway(absoluteEarliestLandingTime, runwayConfiguration);

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

                // Check runway dependency conflicts
                foreach (var dependency in runwayConfiguration.Dependencies)
                {
                    if (!dependency.SeparationSeconds.HasValue)
                        continue;

                    var separationSeconds = dependency.SeparationSeconds.Value;

                    // Find the most recent flight on the dependent runway that could cause a conflict
                    var dependentLeader = sequencedFlights
                        .LastOrDefault(f => f.AssignedRunwayIdentifier == dependency.RunwayIdentifier &&
                                          f.ScheduledLandingTime <= proposedLandingTime);

                    if (dependentLeader is not null)
                    {
                        var requiredSeparationTime = dependentLeader.ScheduledLandingTime.AddSeconds(separationSeconds);
                        if (requiredSeparationTime.IsAfter(proposedLandingTime))
                        {
                            proposedLandingTime = requiredSeparationTime;
                            delayApplied = true;
                        }
                    }

                    // Find any flight on the dependent runway that we would be too close to
                    var dependentTrailer = sequencedFlights
                        .FirstOrDefault(f => f.AssignedRunwayIdentifier == dependency.RunwayIdentifier &&
                                           f.ScheduledLandingTime >= proposedLandingTime);

                    if (dependentTrailer is not null)
                    {
                        var earliestAllowedTime = dependentTrailer.ScheduledLandingTime.AddSeconds(-separationSeconds);
                        if (earliestAllowedTime.IsBefore(proposedLandingTime))
                        {
                            proposedLandingTime = dependentTrailer.ScheduledLandingTime.AddSeconds(separationSeconds);
                            delayApplied = true;
                        }
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

        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && flight.EstimatedFeederFixTime is not null && !flight.HasPassedFeederFix)
        {
            var totalDelay = landingTime - flight.EstimatedLandingTime;
            var feederFixTime = flight.EstimatedFeederFixTime.Value + totalDelay;
            flight.SetFeederFixTime(feederFixTime);
        }
    }

    private static bool IsRunwayEligible(RunwayConfiguration runway, Flight flight)
    {
        if (runway.Requirements?.FeederFixes.Length > 0)
        {
            if (string.IsNullOrEmpty(flight.FeederFixIdentifier))
                return true; // No feeder fix = eligible for any runway

            return runway.Requirements.FeederFixes.Contains(flight.FeederFixIdentifier);
        }

        return true; // No requirements = always eligible
    }
}
