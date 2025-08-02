using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Serilog;

namespace Maestro.Core.Model;

public interface IScheduler
{
    void Schedule(Sequence sequence);
}

// TODO Test Cases:
// - When a flight is added and there is nobody in front of them, no delay is applied
// - When a flight is added and the leader is far away, no delay is applied
// - When a flight is added and the leader is too close, delay is applied
// - When a flight is added and the leader is too close, and other runways are available, other runway is assigned
// - When a flight is added and the leader is too close, all runways result in a delay, the runway with the least delay is assigned
// - When a flight has NoDelay or ManualLandingTime, it should not be delayed
// - When a flight has NoDelay or ManualLandingTime, and the leader is too close, the leader is delayed
// - When a flight has NoDelay or ManualLandingTime, and the leader is too close, and other runways are available, the leader is assigned to the other runway
// - When a flight has NoDelay or ManualLandingTime, and the leader is too close, and the leader is also NoDelay or ManualLandingTime, no delay is applied
// - When a flight is delayed until after a runway change, the new runway and landing rate are used
// - When a new flight is added, with ETAs earlier than a stable flight, the stable flight is delayed
// - When a flight is moved manually, and conflicts with a stable flight, the stable flight is delayed

public class Scheduler(
    IRunwayAssigner runwayAssigner,
    IAirportConfigurationProvider airportConfigurationProvider,
    IPerformanceLookup performanceLookup,
    ILogger logger)
    : IScheduler
{
    public void Schedule(Sequence sequence)
    {
        var sequencableFlights = sequence.Flights
            .Where(f => f.State is not State.Desequenced and not State.Removed)
            .ToList();

        var sequencedFlights = new List<Flight>();
        sequencedFlights.AddRange(sequencableFlights
            .Where(f => f.State is not State.Desequenced and not State.Removed)
            .Where(f => f.State is not State.Unstable)
            .OrderBy(f => f.ScheduledLandingTime));

        // TODO: Insert NoDelay and ManualLandingTime flights last
        // Then then move everyone else around them

        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .Single(c => c.Identifier == sequence.AirportIdentifier);

        var flightsToSchedule = sequencableFlights
            .Where(f => f.State is State.Unstable)
            .OrderBy(f => f.EstimatedLandingTime)
            .ToList();

        foreach (var flight in flightsToSchedule)
        {
            logger.Debug("Scheduling {Callsign}", flight.Callsign);

            var preferredRunways = runwayAssigner.FindBestRunways(
                flight.AircraftType,
                flight.FeederFixIdentifier ?? string.Empty,
                airportConfiguration.RunwayAssignmentRules);

            var currentRunwayMode = sequence.RunwayModeAt(flight.EstimatedLandingTime);

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

                var leader = sequencedFlights.LastOrDefault(f => f.AssignedRunwayIdentifier == runwayIdentifier);
                if (leader is null)
                {
                    // No leader, no delay required
                    proposedLandingTime = flight.EstimatedLandingTime;
                    break;
                }

                var timeToLeader = flight.EstimatedLandingTime - leader.ScheduledLandingTime;
                var acceptanceRate = TimeSpan.FromSeconds(runwayConfiguration.LandingRateSeconds);
                var delayRequired = timeToLeader < acceptanceRate;

                // Do not delay if the flight has NoDelay or ManualLandingTime
                if (delayRequired &&
                    (flight.NoDelay || flight.ManualLandingTime) &&
                    leader is { NoDelay: false, ManualLandingTime: false })
                {
                    // Delay the leader
                    var newLeaderLandingTime = flight.EstimatedLandingTime + acceptanceRate;
                    var newLeaderRunway = leader.AssignedRunwayIdentifier;

                    // TODO: Try moving the leader to another runway if available
                    // TODO: Check if the leader is delayed into a new runway mode

                    Schedule(leader, newLeaderLandingTime, newLeaderRunway);
                }

                if (!delayRequired)
                {
                    // No conflict with the leader, no delay required
                    proposedLandingTime = flight.EstimatedLandingTime;
                    break;
                }

                var newProposedLandingTime = leader.ScheduledLandingTime + acceptanceRate;
                if (proposedLandingTime is null)
                {
                    proposedLandingTime = newProposedLandingTime;
                    proposedRunway = runwayIdentifier;
                    continue;
                }

                // Move to another runway if the delay is less than the current one
                var oldDelay = proposedLandingTime - flight.EstimatedLandingTime;
                var newDelay = newProposedLandingTime - flight.EstimatedLandingTime;
                if (newDelay < oldDelay)
                {
                    // New proposed landing time is better, use it
                    proposedLandingTime = newProposedLandingTime;
                    proposedRunway = runwayIdentifier;
                }
            }

            if (proposedLandingTime is null)
            {
                logger.Warning("Could not schedule {Callsign}", flight.Callsign);
                continue;
            }

            if (sequence.NextRunwayMode is not null && proposedLandingTime.Value.IsSameOrAfter(sequence.RunwayModeChangeTime))
            {
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
            sequencedFlights.Add(flight);
        }
    }

    void Schedule(Flight flight, DateTimeOffset landingTime, string runwayIdentifier)
    {
        flight.SetLandingTime(landingTime, manual: false);
        flight.SetRunway(runwayIdentifier, manual: false);

        if (flight.EstimatedFeederFixTime is not null && !flight.HasPassedFeederFix)
        {
            var totalDelay = landingTime - flight.EstimatedLandingTime;
            var feederFixTime = flight.EstimatedFeederFixTime.Value + totalDelay;
            flight.SetFeederFixTime(feederFixTime);
        }
    }
}
