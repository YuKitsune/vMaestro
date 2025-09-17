using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Serilog;

namespace Maestro.Core.Model;

public interface IScheduler
{
    void Schedule(Sequence sequence, bool recalculateAll = false);
    void Recompute(Flight flight, Sequence sequence);
}

public class Scheduler(
    IRunwayScoreCalculator runwayScoreCalculator,
    IAirportConfigurationProvider airportConfigurationProvider,
    IPerformanceLookup performanceLookup,
    IArrivalLookup arrivalLookup,
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
        var recalculate = recalculateAll;

        // Landed and frozen flights cannot move at all, no action required
        foreach (var flight in sequence.Flights.Where(f => f.State is State.Frozen or State.Landed))
        {
            sequencedFlights.Add(flight);
        }

        // Manual landing time and no-delay are sequenced as-is
        foreach (var flight in sequence.Flights.OrderBy(f => f.LandingTime).Where(f => f.ManualLandingTime || f.NoDelay))
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
        foreach (var flight in sequence.Flights.OrderBy(f => f.LandingEstimate).Where(f => f.HighPriority))
        {
            if (HasBeenScheduled(flight))
                continue;

            ScheduleInternal(airportConfiguration, sequence, flight, flight.LandingEstimate, sequencedFlights);
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

            ScheduleInternal(airportConfiguration, sequence, flight, flight.LandingTime, sequencedFlights);
            sequencedFlights.Add(flight);
            recalculate = true;
        }

        // SuperStable flights generally cannot move, though inserted flights can displace them
        foreach (var flight in sequence.Flights.OrderBy(f => f.LandingEstimate).Where(f => f.State is State.SuperStable))
        {
            if (HasBeenScheduled(flight))
                continue;

            if (recalculate)
                ScheduleInternal(airportConfiguration, sequence, flight, flight.LandingEstimate, sequencedFlights);

            sequencedFlights.Add(flight);
        }

        // New flights can displace Stable flights, but not SuperStable flights
        foreach (var flight in sequence.Flights.OrderBy(f => f.LandingEstimate).Where(f => f.State is State.New))
        {
            if (HasBeenScheduled(flight))
                continue;

            var lastSuperStableFlight = sequencedFlights.LastOrDefault(f => f.State is State.SuperStable);

            ScheduleInternal(airportConfiguration, sequence, flight, lastSuperStableFlight?.LandingTime ?? flight.LandingEstimate, sequencedFlights);
            flight.SetState(State.Unstable, clock);

            sequencedFlights.Add(flight);
            recalculate = true;
        }

        // Stable flights can only be moved if a new flight is inserted in front of them
        foreach (var flight in sequence.Flights.OrderBy(f => f.LandingEstimate).Where(f => f.State is State.Stable))
        {
            if (HasBeenScheduled(flight))
                continue;

            if (recalculate)
                ScheduleInternal(airportConfiguration, sequence, flight, flight.LandingEstimate, sequencedFlights);

            sequencedFlights.Add(flight);
        }

        // Unstable flights are rescheduled every time
        foreach (var flight in sequence.Flights.OrderBy(f => f.LandingEstimate).Where(f => f.State is State.Unstable))
        {
            if (HasBeenScheduled(flight))
                continue;

            ScheduleInternal(airportConfiguration, sequence, flight, flight.LandingEstimate, sequencedFlights);
            sequencedFlights.Add(flight);
        }

        bool HasBeenScheduled(Flight flight)
        {
            return sequencedFlights.Any(f => f.Callsign == flight.Callsign);
        }
    }

    public void Recompute(Flight flight, Sequence sequence)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .Single(c => c.Identifier == sequence.AirportIdentifier);

        var sequencedFlights = new SortedSet<Flight>(FlightComparer.Instance);

        // Add all leaders as-is
        var leaders = sequence.Flights
            .Where(f => f != flight && f.LandingTime.IsSameOrBefore(flight.LandingEstimate))
            .ToList();
        foreach (var leader in leaders)
        {
            sequencedFlights.Add(leader);
        }

        // Schedule just this flight, ensuring enough separation from leaders
        ScheduleInternal(airportConfiguration, sequence, flight, flight.LandingEstimate, sequencedFlights);

        // Recalculate the rest of the sequence
        Schedule(sequence, recalculateAll: true);
    }

    void ScheduleInternal(
        AirportConfiguration airportConfiguration,
        Sequence sequence,
        Flight flight,
        DateTimeOffset absoluteEarliestLandingTime,
        SortedSet<Flight> sequencedFlights)
    {
        logger.Debug("Scheduling {Callsign}", flight.Callsign);

        var currentRunwayMode = sequence.GetRunwayModeAt(absoluteEarliestLandingTime);

tryAgain:
        var preferredRunwaysInMode = FindEligibleRunways(
            flight,
            currentRunwayMode);

        DateTimeOffset? proposedLandingTime = null;
        Runway? proposedRunway = null;
        foreach (var runway in preferredRunwaysInMode)
        {
            var earliestLandingTime = GetEarliestLandingTimeForRunway(
                absoluteEarliestLandingTime,
                runway,
                sequencedFlights);

            // Check if this runway has slot conflicts
            var conflictingSlot = sequence.Slots.FirstOrDefault(s =>
                s.RunwayIdentifiers.Contains(runway.Identifier) && s.StartTime.IsBefore(earliestLandingTime) && s.EndTime.IsAfter(earliestLandingTime));
            if (conflictingSlot is not null)
            {
                earliestLandingTime = GetEarliestLandingTimeForRunway(
                    conflictingSlot.EndTime,
                    runway,
                    sequencedFlights);
            }

            // If this runway results in less delay, then use that one
            if (proposedLandingTime is null || earliestLandingTime.IsBefore(proposedLandingTime.Value))
            {
                proposedLandingTime = earliestLandingTime;
                proposedRunway = runway;
            }
        }

        if (proposedLandingTime is null || proposedRunway is null)
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
        if (performance is not null && performance.AircraftCategory == AircraftCategory.Jet && proposedLandingTime.Value.IsAfter(flight.LandingEstimate))
        {
            flight.SetFlowControls(FlowControls.ReduceSpeed);
        }
        else
        {
            flight.SetFlowControls(FlowControls.ProfileSpeed);
        }

        Schedule(flight, proposedLandingTime.Value, proposedRunway.Identifier, performance);
    }

    IEnumerable<Runway> FindEligibleRunways(Flight flight, RunwayMode runwayMode)
    {
        // TODO: Choose runways based on FeederFix
        if (flight.RunwayManuallyAssigned && !string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
            return runwayMode.Runways.Where(r => r.Identifier == flight.AssignedRunwayIdentifier);

        var eligibleRunways = runwayMode.Runways.Where(r => IsRunwayEligible(r, flight)).ToArray();

        var preferredRunwaysInMode = runwayScoreCalculator.CalculateScores(
                eligibleRunways,
                flight.AircraftType,
                flight.WakeCategory,
                flight.FeederFixIdentifier)
            .Select(rs => runwayMode.Runways.SingleOrDefault(rc => rs.RunwayIdentifier == rc.Identifier))
            .WhereNotNull();

        return preferredRunwaysInMode;
    }

    DateTimeOffset GetEarliestLandingTimeForRunway(
        DateTimeOffset startTime,
        Runway runway,
        SortedSet<Flight> sequencedFlights)
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
            var leader = sequencedFlights.LastOrDefault(f => f.AssignedRunwayIdentifier == runway.Identifier && f.LandingTime <= proposedLandingTime);
            if (leader is not null)
            {
                var nextLandingTimeAfterLeader = leader.LandingTime.Add(runway.AcceptanceRate);
                if (nextLandingTimeAfterLeader.IsAfter(proposedLandingTime))
                {
                    proposedLandingTime = nextLandingTimeAfterLeader;
                    delayApplied = true;
                }
            }

            // Check trailer conflict
            var trailer = sequencedFlights.FirstOrDefault(f => f.AssignedRunwayIdentifier == runway.Identifier && f.LandingTime >= proposedLandingTime);
            if (trailer is not null)
            {
                var lastLandingTimeBeforeTrailer = trailer.LandingTime.Subtract(runway.AcceptanceRate);
                if (lastLandingTimeBeforeTrailer.IsBefore(proposedLandingTime))
                {
                    proposedLandingTime = trailer.LandingTime.Add(runway.AcceptanceRate);
                    delayApplied = true;
                }
            }

            // Check runway dependency conflicts
            foreach (var dependency in runway.Dependencies)
            {
                if (!dependency.Separation.HasValue)
                    continue;

                var separation = dependency.Separation.Value;

                // Find the most recent flight on the dependent runway that could cause a conflict
                var dependentLeader = sequencedFlights
                    .LastOrDefault(f => f.AssignedRunwayIdentifier == dependency.RunwayIdentifier &&
                                      f.LandingTime <= proposedLandingTime);

                if (dependentLeader is not null)
                {
                    var requiredSeparationTime = dependentLeader.LandingTime.Add(separation);
                    if (requiredSeparationTime.IsAfter(proposedLandingTime))
                    {
                        proposedLandingTime = requiredSeparationTime;
                        delayApplied = true;
                    }
                }

                // Find any flight on the dependent runway that we would be too close to
                var dependentTrailer = sequencedFlights
                    .FirstOrDefault(f => f.AssignedRunwayIdentifier == dependency.RunwayIdentifier &&
                                       f.LandingTime >= proposedLandingTime);

                if (dependentTrailer is not null)
                {
                    var earliestAllowedTime = dependentTrailer.LandingTime.Subtract(separation);
                    if (earliestAllowedTime.IsBefore(proposedLandingTime))
                    {
                        proposedLandingTime = dependentTrailer.LandingTime.Add(separation);
                        delayApplied = true;
                    }
                }
            }
        } while (delayApplied);

        return proposedLandingTime;
    }

    void Schedule(Flight flight, DateTimeOffset landingTime, string runwayIdentifier, AircraftPerformanceData performanceData)
    {
        flight.SetLandingTime(landingTime);
        flight.SetRunway(runwayIdentifier, manual: flight.RunwayManuallyAssigned);

        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && flight.FeederFixEstimate is not null && !flight.HasPassedFeederFix)
        {
            var arrivalInterval = arrivalLookup.GetArrivalInterval(
                flight.DestinationIdentifier,
                flight.FeederFixIdentifier,
                flight.AssignedArrivalIdentifier,
                flight.AssignedRunwayIdentifier,
                performanceData);
            if (arrivalInterval is not null)
            {
                var feederFixTime = flight.LandingTime.Subtract(arrivalInterval.Value);
                flight.SetFeederFixTime(feederFixTime);
            }
            else
            {
                logger.Warning("Could not update feeder fix time for {Callsign}, no arrival interval found", flight.Callsign);
            }
        }

        flight.ResetInitialEstimates();
    }

    private static bool IsRunwayEligible(Runway runway, Flight flight)
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
