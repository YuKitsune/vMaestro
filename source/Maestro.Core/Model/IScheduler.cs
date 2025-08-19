using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
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

        void ScheduleInternal(Flight flight)
        {
            logger.Debug("Scheduling {Callsign}", flight.Callsign);

            var currentRunwayMode = sequence.RunwayModeAt(flight.EstimatedLandingTime);
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

                var earliestLandingTime = GetEarliestLandingTimeForRunway(flight, runwayConfiguration);

                // If this runway results in less delay, then use that one
                if (proposedLandingTime is null || earliestLandingTime.IsSameOrBefore(proposedLandingTime.Value))
                {
                    proposedLandingTime = earliestLandingTime;
                    proposedRunway = runwayConfiguration.Identifier;
                }
            }

            if (sequence.NextRunwayMode is not null && proposedLandingTime.Value.IsSameOrAfter(sequence.RunwayModeChangeTime))
            {
                logger.Debug("Flight {Callsign} delayed beyond runway mode change, trying new mode", flight.Callsign);

                currentRunwayMode = sequence.NextRunwayMode;

                goto tryAgain;
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
            flight.SetState(State.Unstable);
        }

        DateTimeOffset GetEarliestLandingTimeForRunway(Flight flight, RunwayConfiguration runwayConfiguration)
        {
            var proposedLandingTime = flight.EstimatedLandingTime;

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
                    throw new MaestroException($"{flight.Callsign} exceeded max conflict resolution attempts");
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

//     public void Schedule(Sequence sequence)
//     {
//         var sequencableFlights = sequence.Flights
//             .Where(f => f.State is not State.Desequenced and not State.Removed)
//             .ToList();
//
//         // BUG: If a stable flight is behind any unstable ones, all the unstable flights get pushed back
//
//         var sequencedFlights = new List<Flight>();
//         sequencedFlights.AddRange(sequencableFlights
//             .Where(f => f.State is State.SuperStable or State.Frozen or State.Landed)
//             .OrderBy(f => f.ScheduledLandingTime));
//
//         // TODO: Insert NoDelay and ManualLandingTime flights last
//         // Then then move everyone else around them
//
//         var airportConfiguration = airportConfigurationProvider
//             .GetAirportConfigurations()
//             .Single(c => c.Identifier == sequence.AirportIdentifier);
//
//         var flightsToSchedule = sequencableFlights
//             .Where(f => f.State is State.New or State.Unstable or State.Stable)
//             .OrderBy(f => f.EstimatedLandingTime)
//             .ToList();
//
//         foreach (var flight in flightsToSchedule)
//         {
//             logger.Debug("Scheduling {Callsign}", flight.Callsign);
//
//             var currentRunwayMode = sequence.RunwayModeAt(flight.EstimatedLandingTime);
//             var runwayModeChangeAttempts = 0;
//             const int maxRunwayModeChangeAttempts = 2;
//
//             var preferredRunways = flight.RunwayManuallyAssigned && !string.IsNullOrEmpty(flight.AssignedRunwayIdentifier)
//                 ? [flight.AssignedRunwayIdentifier!] // Use only the manually assigned runway
//                 : runwayAssigner.FindBestRunways(
//                     flight.AircraftType,
//                     flight.FeederFixIdentifier ?? string.Empty,
//                     airportConfiguration.RunwayAssignmentRules);
//
// tryAgain:
//             DateTimeOffset? proposedLandingTime = null;
//             var proposedRunway = preferredRunways.First();
//             var alreadyScheduled = false;
//             foreach (var runwayIdentifier in preferredRunways)
//             {
//                 var runwayConfiguration = currentRunwayMode.Runways
//                     .SingleOrDefault(r => r.Identifier == runwayIdentifier);
//                 if (runwayConfiguration is null)
//                 {
//                     // Runway not in mode
//                     continue;
//                 }
//
//                 var leader = sequencedFlights.LastOrDefault(f => f.AssignedRunwayIdentifier == runwayIdentifier);
//                 if (leader is null)
//                 {
//                     // No leader, no delay required
//                     proposedLandingTime = flight.EstimatedLandingTime;
//                     proposedRunway = runwayIdentifier;
//                     break;
//                 }
//
//                 // Use manual landing time if flight has one, otherwise use estimated time for conflict detection
//                 var flightTime = flight.ManualLandingTime ? flight.ScheduledLandingTime : flight.EstimatedLandingTime;
//                 var timeToLeader = flightTime - leader.ScheduledLandingTime;
//                 var acceptanceRate = TimeSpan.FromSeconds(runwayConfiguration.LandingRateSeconds);
//                 var delayRequired = timeToLeader < acceptanceRate;
//
//                 // Special case: Both flights have NoDelay or ManualLandingTime - no delay applied to either
//                 if (delayRequired &&
//                     (flight.NoDelay || flight.ManualLandingTime) &&
//                     (leader.NoDelay || leader.ManualLandingTime))
//                 {
//                     // Neither flight should be delayed - schedule at estimated time
//                     proposedLandingTime = flight.EstimatedLandingTime;
//                     break;
//                 }
//
//                 // Do not delay if the flight has NoDelay or ManualLandingTime
//                 // OR if the flight is New (but not HighPriority) and the leader is only Stable (not SuperStable/Frozen/Landed)
//                 if (delayRequired &&
//                     ((flight.NoDelay || flight.ManualLandingTime) ||
//                      (flight.State == State.New && !flight.HighPriority && leader.State == State.Stable)) &&
//                     leader is { NoDelay: false, ManualLandingTime: false } &&
//                     leader.State is not State.SuperStable and not State.Frozen and not State.Landed)
//                 {
//                     // Delay the leader
//                     var newLeaderLandingTime = flightTime + acceptanceRate;
//                     var newLeaderRunway = leader.AssignedRunwayIdentifier;
//
//                     // TODO: Try moving the leader to another runway if available
//                     // TODO: Check if the leader is delayed into a new runway mode
//
//                     Schedule(leader, newLeaderLandingTime, newLeaderRunway);
//
//                     // Insert the current flight before the delayed leader
//                     var leaderIndex = sequencedFlights.IndexOf(leader);
//                     if (leaderIndex >= 0)
//                     {
//                         // Use the same flight time we calculated for conflict detection
//                         Schedule(flight, flightTime, runwayIdentifier);
//                         sequencedFlights.Insert(leaderIndex, flight);
//                         alreadyScheduled = true;
//                         break; // Exit the runway loop and continue to next flight
//                     }
//                 }
//
//                 if (!delayRequired)
//                 {
//                     // No conflict with the leader, no delay required
//                     proposedLandingTime = flight.EstimatedLandingTime;
//                     break;
//                 }
//
//                 var newProposedLandingTime = leader.ScheduledLandingTime + acceptanceRate;
//                 if (proposedLandingTime is null)
//                 {
//                     proposedLandingTime = newProposedLandingTime;
//                     proposedRunway = runwayIdentifier;
//                     continue;
//                 }
//
//                 // Move to another runway if the delay is less than the current one
//                 var oldDelay = proposedLandingTime - flight.EstimatedLandingTime;
//                 var newDelay = newProposedLandingTime - flight.EstimatedLandingTime;
//                 if (newDelay < oldDelay)
//                 {
//                     // New proposed landing time is better, use it
//                     proposedLandingTime = newProposedLandingTime;
//                     proposedRunway = runwayIdentifier;
//                 }
//             }
//
//             if (alreadyScheduled)
//             {
//                 continue;
//             }
//
//             if (proposedLandingTime is null)
//             {
//                 logger.Warning("Could not schedule {Callsign}", flight.Callsign);
//                 continue;
//             }
//
//             if (sequence.NextRunwayMode is not null &&
//                 proposedLandingTime.Value.IsSameOrAfter(sequence.RunwayModeChangeTime) &&
//                 runwayModeChangeAttempts < maxRunwayModeChangeAttempts)
//             {
//                 logger.Debug("Flight {Callsign} delayed beyond runway mode change, trying new mode (attempt {Attempt})",
//                     flight.Callsign, runwayModeChangeAttempts + 1);
//
//                 currentRunwayMode = sequence.NextRunwayMode;
//                 runwayModeChangeAttempts++;
//
//                 goto tryAgain;
//             }
//
//             if (sequence.NextRunwayMode is not null &&
//                 proposedLandingTime.Value.IsSameOrAfter(sequence.RunwayModeChangeTime) &&
//                 runwayModeChangeAttempts >= maxRunwayModeChangeAttempts)
//             {
//                 logger.Warning("Flight {Callsign} could not be rescheduled after runway mode change attempts", flight.Callsign);
//             }
//
//             // TODO: Double check how this is supposed to work
//             var performance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
//             if (performance is not null && performance.AircraftCategory == AircraftCategory.Jet && proposedLandingTime.Value.IsAfter(flight.EstimatedLandingTime))
//             {
//                 flight.SetFlowControls(FlowControls.S250);
//             }
//             else
//             {
//                 flight.SetFlowControls(FlowControls.ProfileSpeed);
//             }
//
//             // Use manual landing time if the flight has one, otherwise use proposed time
//             var finalLandingTime = flight.ManualLandingTime ? flight.ScheduledLandingTime : proposedLandingTime.Value;
//             Schedule(flight, finalLandingTime, proposedRunway);
//             sequencedFlights.Add(flight);
//         }
//
//         // Transition New flights to Unstable after scheduling
//         foreach (var flight in sequencableFlights.Where(f => f.State == State.New))
//         {
//             flight.SetState(State.Unstable);
//         }
//     }

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
}
