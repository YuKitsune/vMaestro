using Maestro.Core.Configuration;
using Serilog;

namespace Maestro.Core.Model;

public interface ISlotBasedScheduler
{
    void Schedule(SlotBasedSequence sequence);
    void AllocateSlot(SlotBasedSequence sequence, Flight flight);
}

// TODO: Handle no-delay and priority flights
// TODO: Handle manual landing times
// TODO: Account for flights not yet in the sequence regardless of state
// TODO: Consider de-allocating all the unstable slots so they get a fresh start every time
// TODO: Runway and terminal trajectories
// BUG: Recompute is not respected here.

public class SlotBasedScheduler(
    IRunwayAssigner runwayAssigner,
    IAirportConfigurationProvider airportConfigurationProvider,
    IPerformanceLookup performanceLookup,
    ILogger logger)
    : ISlotBasedScheduler
{
    public void Schedule(SlotBasedSequence sequence)
    {
        var slotsWithUnstableFlights = sequence.Slots
            .Where(s =>
                s.Flight is not null &&
                s.Flight.State == State.Unstable &&
                !s.Flight.NoDelay &&
                !s.Flight.ManualLandingTime)
            .ToList();

        var unstableFlights = slotsWithUnstableFlights
            .Select(s => s.Flight!)
            .ToList();

        // Deallocate unstable flights to allow for rescheduling
        foreach (var slotsWithUnstableFlight in slotsWithUnstableFlights)
        {
            slotsWithUnstableFlight.Deallocate();
        }

        foreach (var flight in unstableFlights)
        {
            logger.Information("Scheduling {Callsign}.", flight.Callsign);
            AllocateSlot(sequence, flight);
        }
    }

    public void AllocateSlot(SlotBasedSequence sequence, Flight flight)
    {
        var allocatedSlot = sequence.FindSlotFor(flight.Callsign);
        if (allocatedSlot is not null)
        {
            allocatedSlot.Deallocate();
        }

        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .Single(c => c.Identifier == sequence.AirportIdentifier);

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

            // Try to push the flight forward to the preceeding slot if it's available
            var delay = nextBestSlot.Time - flight.EstimatedLandingTime;
            if (delay >= TimeSpan.FromMinutes(1) && delay < nextBestSlot.Duration)
            {
                var precedingSlot = FindPrecedingSlot(sequence, nextBestSlot, matchingRunway);
                if (precedingSlot is not null && precedingSlot.IsAvailable)
                {
                    nextBestSlot = precedingSlot;
                }
            }

            if (bestSlot is null)
            {
                bestSlot = nextBestSlot;
                continue;
            }

            // Suggest the lower priority runway if it results in less delay
            var newDelay = nextBestSlot.Time - flight.EstimatedLandingTime;
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

    Slot? FindPrecedingSlot(SlotBasedSequence sequence, Slot slot, string runwayIdentifier)
    {
        var slotsForRunway = sequence.Slots
            .Where(s => s.RunwayIdentifier == runwayIdentifier)
            .ToArray();

        var slotIndex = Array.IndexOf(slotsForRunway, slot);
        return slotIndex < 0
            ? null
            : sequence.Slots[slotIndex - 1];
    }
}
