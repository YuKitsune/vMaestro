using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Serilog;

namespace Maestro.Core.Model;

public interface ISlotBasedScheduler
{
    void Schedule(SlotBasedSequence sequence);
}

// TODO Test Cases:
// - When a new flight is added, before a stable flight, the stable flight can be delayed
// - When a flight has NoDelay or ManualLandingTime, it should not be delayed
// - When a flight has NoDelay or ManualLandingTime, and the leader is too close, the leader is delayed
// - When a flight has NoDelay or ManualLandingTime, and the leader is too close, and the leader is also NoDelay or ManualLandingTime, no delay is applied
// - No leaders, no delay
// - If a flight is delayed until after a runway change, the new landing rate is used
// - If a lower priority runway results in less delay, it is reassigned

public class SlotBasedScheduler(
    IRunwayAssigner runwayAssigner,
    IAirportConfigurationProvider airportConfigurationProvider,
    IPerformanceLookup performanceLookup,
    ILogger logger)
    : ISlotBasedScheduler
{
    public void Schedule(SlotBasedSequence sequence)
    {
        var sequencedFlights = new List<Flight>();

        // Add all stable flights so we don't modify their landing times
        sequencedFlights.AddRange(sequence.Flights
            .Where(f => f.State is not State.Unstable)
            .OrderBy(f => f.ScheduledLandingTime));

        var unstableFlights = sequence.Flights
            .Where(s => s.State == State.Unstable)
            .OrderBy(s => s.EstimatedLandingTime)
            .ToList();

        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .Single(c => c.Identifier == sequence.AirportIdentifier);

        foreach (var flight in unstableFlights)
        {
            logger.Information("Scheduling {Callsign}.", flight.Callsign);

            // NoDelay and ManualLandingTime flights are skipped
            // Consider them fixed in the sequence and move everyone around them.
            if (flight.NoDelay || flight.ManualLandingTime)
            {
                var leader = sequencedFlights.LastOrDefault(f =>
                    !f.NoDelay &&
                    !f.ManualLandingTime &&
                    f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier);
                if (leader is not null)
                {
                    var runwayMode = sequence.RunwayModeAt(flight.ScheduledLandingTime);
                    var acceptanceRate = TimeSpan.FromSeconds(runwayMode.Runways
                        .Single(r => r.Identifier == flight.AssignedRunwayIdentifier)
                        .LandingRateSeconds);
                    if (leader.ScheduledLandingTime.Add(acceptanceRate).IsBefore(flight.EstimatedLandingTime))
                    {
                        var newLeaderLandingTime = leader.ScheduledLandingTime + acceptanceRate;
                        Schedule(leader, newLeaderLandingTime, leader.AssignedRunwayIdentifier);
                    }
                }

                sequencedFlights.Add(flight);
                continue;
            }

            var matchingRunways = runwayAssigner.FindBestRunways(
                flight.AircraftType,
                flight.FeederFixIdentifier,
                airportConfiguration.RunwayAssignmentRules);

            DateTimeOffset? bestLandingTime = null;
            var assignedRunway = matchingRunways.First();
            foreach (var matchingRunway in matchingRunways)
            {
                var leaderOnRunway = sequencedFlights.LastOrDefault(f => f.AssignedRunwayIdentifier == matchingRunway);
                if (leaderOnRunway is null)
                {
                    // No leader on this runway, no delay required
                    bestLandingTime = flight.EstimatedLandingTime;
                    break;
                }

                var landingRate = DetermineLandingRate(sequence, leaderOnRunway.ScheduledLandingTime, matchingRunway);
                var nextBestLandingTime = leaderOnRunway.ScheduledLandingTime + landingRate;
                if (bestLandingTime is null)
                {
                    bestLandingTime = nextBestLandingTime;
                    continue;
                }

                // Suggest the lower priority runway if it results in less delay
                var newDelay = nextBestLandingTime - flight.EstimatedLandingTime;
                var currentDelay = bestLandingTime - flight.EstimatedLandingTime;
                if (newDelay < currentDelay)
                {
                    bestLandingTime = nextBestLandingTime;
                    assignedRunway = matchingRunway;
                }
            }

            if (bestLandingTime is null)
            {
                throw new MaestroException($"Landing time for {flight.Callsign} could not be determined.");
            }

            if (string.IsNullOrEmpty(assignedRunway))
            {
                throw new MaestroException($"Runway for {flight.Callsign} could not be determined.");
            }

            if (assignedRunway != matchingRunways.FirstOrDefault())
            {
                logger.Information(
                    "{Callsign} assigned runway {RunwayIdentifier} for a reduced delay",
                    flight.Callsign,
                    assignedRunway);
            }

            Schedule(flight, bestLandingTime.Value, assignedRunway);

            // TODO: Revisit flow controls
            var performance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
            if (performance is not null &&
                performance.AircraftCategory == AircraftCategory.Jet &&
                flight.EstimatedLandingTime < bestLandingTime.Value)
            {
                flight.SetFlowControls(FlowControls.S250);
            }
            else
            {
                flight.SetFlowControls(FlowControls.ProfileSpeed);
            }

            sequencedFlights.Add(flight);

            logger.Information(
                "{Callsign} STA_FF {ScheduledFeederFixTime:HH:mm}, total delay {TotalDelay:N0} mins)",
                flight.Callsign,
                flight.ScheduledFeederFixTime,
                flight.TotalDelay.TotalMinutes);
        }
    }

    TimeSpan DetermineLandingRate(SlotBasedSequence sequence, DateTimeOffset leaderLandingTime, string runwayIdentifier)
    {
        var currentLandingRateSeconds = sequence.CurrentRunwayMode.Runways
            .Single(r => r.Identifier == runwayIdentifier)
            .LandingRateSeconds;

        // No runway change is planned, use the current acceptance rate
        if (sequence.NextRunwayMode is null)
        {
            return TimeSpan.FromSeconds(currentLandingRateSeconds);
        }

        var nextLandingRateSeconds = sequence.NextRunwayMode.Runways
            .Single(r => r.Identifier == runwayIdentifier)
            .LandingRateSeconds;

        // If the current acceptance rate pushes us into the next runway mode, use the next acceptance rate
        if (leaderLandingTime.AddSeconds(currentLandingRateSeconds).IsAfter(sequence.RunwayModeChangeTime) &&
            leaderLandingTime.AddSeconds(nextLandingRateSeconds).IsAfter(sequence.RunwayModeChangeTime))
        {
            return TimeSpan.FromSeconds(nextLandingRateSeconds);
        }

        return TimeSpan.FromSeconds(currentLandingRateSeconds);
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
