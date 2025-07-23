using Maestro.Core.Configuration;
using Serilog;

namespace Maestro.Core.Model;

public interface ISlotBasedScheduler
{
    void AllocateSlot(SlotBasedSequence sequence, Flight flight);
}

public class SlotBasedScheduler(IRunwayAssigner runwayAssigner, IAirportConfigurationProvider airportConfigurationProvider, IPerformanceLookup performanceLookup, ILogger logger)
{
    void AllocateSlot(SlotBasedSequence sequence, Flight flight)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .Single(c => c.Identifier == sequence.AirportIdentifier);

        // TODO: Handle no-delay and priority flights
        // TODO: Handle manual landing times
        // TODO: Account for flights not yet in the sequence regardless of state
        // TODO: Consider de-allocating all the unstable slots so they get a fresh start every time
        // TODO: Don't filter by runway, assign runway as part of allocating the slot

        var lastSuperStableSlotIndex = sequence.Slots
            .OrderBy(s => s.Time)
            .ToList()
            .FindLastIndex(s =>
                s.RunwayIdentifier == flight.AssignedRunwayIdentifier &&
                s.IsAvailable &&
                (s.Flight?.PositionIsFixed ?? false));

        var earliestAvailableSlot = sequence.Slots
            .Skip(lastSuperStableSlotIndex)
            .FirstOrDefault(s =>
                s.IsAvailable &&
                s.RunwayIdentifier == flight.AssignedRunwayIdentifier &&
                s.Time >= flight.EstimatedLandingTime);

        if (earliestAvailableSlot is null)
        {
            logger.Warning("No available slots for {Callsign}. Cannot schedule.", flight.Callsign);
            return;
        }

        // TODO: Optimisation (If a lower priority runway would result in less delay, use that instead)
        var runway = FindBestRunway(
            flight,
            sequence.RunwayModeAt(earliestAvailableSlot.Time),
            airportConfiguration.RunwayAssignmentRules);

        earliestAvailableSlot.AllocateTo(flight);

        var performance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        if (performance is not null && performance.AircraftCategory == AircraftCategory.Jet && flight.EstimatedLandingTime < earliestAvailableSlot.Time)
        {
            flight.SetFlowControls(FlowControls.S250);
        }
        else
        {
            flight.SetFlowControls(FlowControls.ProfileSpeed);
        }

        logger.Information(
            "{Callsign} allocated to slot {Slot} (STA_FF {ScheduledFeederFixTime:HH:mm}, total delay {TotalDelay:N0} mins)",
            flight.Callsign,
            earliestAvailableSlot,
            flight.ScheduledFeederFixTime,
            flight.TotalDelay.TotalMinutes);
    }

    string FindBestRunway(
        Flight flight,
        RunwayMode runwayMode,
        IReadOnlyCollection<RunwayAssignmentRule> assignmentRules)
    {
        var defaultRunway = runwayMode.Runways.First().Identifier;
        if (string.IsNullOrEmpty(flight.FeederFixIdentifier))
            return defaultRunway;

        var possibleRunways = runwayAssigner.FindBestRunways(
            flight.AircraftType,
            flight.FeederFixIdentifier,
            assignmentRules);

        var runwaysInMode = possibleRunways
            .Where(r => runwayMode.Runways.Any(r2 => r2.Identifier == r))
            .ToArray();

        // No runways found, use the default one
        if (!runwaysInMode.Any())
            return defaultRunway;

        // TODO: Use lower priorities depending on traffic load.
        //  How could we go about this? Probe for shortest delay? Round-robin?
        var topPriority = runwaysInMode.First();
        return topPriority;
    }
}
