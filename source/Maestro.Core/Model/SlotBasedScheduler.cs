using Maestro.Core.Configuration;
using Serilog;

namespace Maestro.Core.Model;

public interface ISlotBasedScheduler
{
    void AllocateSlot(SlotBasedSequence sequence, Flight flight);
}

// Test cases:
// - When no slots are allocated, the flight should be allocated the slot closest to their landing estimate.
// - When multiple flights share the same landing estimate, they are assigned different slots.
// - When multiple runways are available, and the less preferred runway results in a lower delay, it should be allocated.
// - When delaying a jet, the flight should be set to S250.
// - When delaying a non-jet, the flight should be set to ProfileSpeed.

public class SlotBasedScheduler(
    IRunwayAssigner runwayAssigner,
    IAirportConfigurationProvider airportConfigurationProvider,
    IPerformanceLookup performanceLookup,
    ILogger logger)
    : ISlotBasedScheduler
{
    public void AllocateSlot(SlotBasedSequence sequence, Flight flight)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .Single(c => c.Identifier == sequence.AirportIdentifier);

        // TODO: Handle no-delay and priority flights
        // TODO: Handle manual landing times
        // TODO: Account for flights not yet in the sequence regardless of state
        // TODO: Consider de-allocating all the unstable slots so they get a fresh start every time

        var matchingRunways = runwayAssigner.FindBestRunways(
            flight.AircraftType,
            flight.FeederFixIdentifier,
            airportConfiguration.RunwayAssignmentRules);

        Slot? bestSlot = null;
        foreach (var matchingRunway in matchingRunways)
        {
            var availableSlotsForRunway = FindPossibleSlots(sequence, flight, matchingRunway);
            if (availableSlotsForRunway.Length == 0)
                continue;

            var nextBestSlot = availableSlotsForRunway.FirstOrDefault();
            if (nextBestSlot is null)
                continue;

            var newDelay = nextBestSlot.Time - flight.EstimatedLandingTime;

            if (bestSlot is null)
            {
                bestSlot = nextBestSlot;
                continue;
            }

            // Suggest the lower priority runway if it results in less delay
            var currentDelay = bestSlot.Time - flight.EstimatedLandingTime;
            if (newDelay < currentDelay)
            {
                bestSlot = nextBestSlot;
            }
        }

        if (bestSlot is null)
        {
            logger.Warning("No available slots for {Callsign}. Cannot schedule.", flight.Callsign);
            return;
        }

        if (bestSlot.RunwayIdentifier != matchingRunways.FirstOrDefault())
        {
            logger.Information(
                "{Callsign} assigned runway {RunwayIdentifier} for a reduced delay",
                flight.Callsign,
                bestSlot.RunwayIdentifier);
        }

        bestSlot.AllocateTo(flight);

        var performance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        if (performance is not null &&
            performance.AircraftCategory == AircraftCategory.Jet &&
            flight.EstimatedLandingTime < bestSlot.Time)
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
            bestSlot,
            flight.ScheduledFeederFixTime,
            flight.TotalDelay.TotalMinutes);
    }

    Slot[] FindPossibleSlots(SlotBasedSequence sequence, Flight flight, string runwayIdentifier)
    {
        var slotsForRunway = sequence.Slots
            .OrderBy(s => s.Time)
            .Where(s => s.RunwayIdentifier == runwayIdentifier)
            .ToList();

        var lastSuperStableSlotIndex = slotsForRunway
            .FindLastIndex(s =>
                s.IsAvailable &&
                (s.Flight?.PositionIsFixed ?? false));

        lastSuperStableSlotIndex = Math.Max(0, lastSuperStableSlotIndex);
        var availableSlots = slotsForRunway
            .Skip(lastSuperStableSlotIndex)
            .Where(s =>
                s.IsAvailable &&
                s.Time >= flight.EstimatedLandingTime);

        return availableSlots.ToArray();
    }
}
