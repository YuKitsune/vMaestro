using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Model;

namespace Maestro.Core;

public class Scheduler(IArrivalLookup arrivalLookup)
{
    readonly IArrivalLookup _arrivalLookup = arrivalLookup;

    public void Schedule(
        AirportConfiguration airportConfiguration,
        NewSequence sequence,
        int startIndex,
        string runwayIdentifier)
    {
        for (var i = startIndex; i < sequence.Items.Count; i++)
        {
            if (i == 0)
                continue;

            var currentItem = sequence.Items[i];
            if (currentItem is not FlightSequenceItem flightItem)
            {
                continue;
            }

            var currentFlight = flightItem.Flight;
            if (currentFlight.State is State.Landed or State.Frozen)
            {
                continue;
            }

            // Stable and SuperStable flights should not have their landing times changed
            // unless we're forcing a reschedule due to operational changes
            // if (currentFlight.State is State.Stable or State.SuperStable && !forceRescheduleStable)
            // {
            //     continue;
            // }

            if (runwayIdentifier != currentFlight.AssignedRunwayIdentifier)
            {
                continue;
            }

            // TODO: Source the runway mode information from the flight
            var runwayModeItem = sequence.Items
                .Take(i)
                .OfType<RunwayModeChangeSequenceItem>()
                .LastOrDefault();
            if (runwayModeItem is null)
                throw new Exception("No runway mode found");

            var currentRunwayMode = runwayModeItem.RunwayMode;

            // If the assigned runway isn't in the current mode, use the default
            Runway runway;
            if (currentFlight.FeederFixIdentifier is not null && !currentFlight.RunwayManuallyAssigned)
            {
                if (airportConfiguration.PreferredRunways.TryGetValue(currentFlight.FeederFixIdentifier,
                        out var preferredRunways))
                {
                    runway = currentRunwayMode.Runways
                                 .FirstOrDefault(r => preferredRunways.Contains(r.Identifier))
                             ?? currentRunwayMode.Default;
                }
                else
                {
                    runway = currentRunwayMode.Default;
                }
            }
            else
            {
                runway = currentRunwayMode.Runways
                             .FirstOrDefault(r => r.Identifier == currentFlight.AssignedRunwayIdentifier)
                         ?? currentRunwayMode.Default;
            }

            // Determine the earliest possible landing time based on the preceding item on this runway
            var precedingItemsOnRunway = sequence.Items
                .Take(i)
                .Where(s => AppliesToRunway(s, runway))
                .ToList();

            var previousItem = precedingItemsOnRunway.Last();
            var earliestLandingTimeFromPrevious = previousItem switch
            {
                FlightSequenceItem previousFlightItem => previousFlightItem.Flight.LandingTime.Add(runway.AcceptanceRate),
                SlotSequenceItem slotItem => slotItem.Slot.EndTime,
                RunwayModeChangeSequenceItem runwayModeChange => runwayModeChange.FirstLandingTimeInNewMode,
                _ => throw new ArgumentOutOfRangeException()
            };

            // If the previous item is a frozen flight, look ahead for any slots they may be occupying
            if (previousItem is FlightSequenceItem { Flight.State: State.Frozen })
            {
                var lastSlot = precedingItemsOnRunway
                    .OfType<SlotSequenceItem>()
                    .LastOrDefault();
                if (lastSlot is not null)
                {
                    earliestLandingTimeFromPrevious = lastSlot.Slot.EndTime.IsAfter(earliestLandingTimeFromPrevious)
                        ? lastSlot.Slot.EndTime
                        : earliestLandingTimeFromPrevious;
                }
            }

            // Don't speed flights up to land before their estimate
            var landingTime = earliestLandingTimeFromPrevious.IsAfter(currentFlight.LandingEstimate)
                ? earliestLandingTimeFromPrevious
                : currentFlight.LandingEstimate;

            // Ensure manual delay flights aren't delayed by more than their maximum delay
            if (currentFlight.MaximumDelay is not null)
            {
                var totalDelay = landingTime - currentFlight.LandingEstimate;

                // Zero delay flights can be delayed within the acceptance rate, but no more
                // E.g. QFA1 lands at T15, QFA2 is Zero delay estimating at T17. Rather than moving QFA1 back and giving them a 5-minute delay, give QFA2 a 1-minute delay instead
                if (currentFlight.MaximumDelay == TimeSpan.Zero && totalDelay < runway.AcceptanceRate)
                {
                }
                else if (previousItem is FlightSequenceItem { Flight.State: State.Frozen or State.Landed })
                {
                    // Don't move in front of frozen or landed flights
                }
                else if (totalDelay > currentFlight.MaximumDelay)
                {
                    // Delay exceeds the maximum, move this flight forward one space and reprocess
                    var previousItemIndex = sequence.IndexOf(previousItem);
                    if (previousItemIndex != -1)
                    {
                        sequence.Swap(i, previousItemIndex);
                        i = previousItemIndex - 1;
                        continue;
                    }
                }
            }

            // Check if this landing time would conflict with the next item in the sequence
            var nextItem = sequence.Items
                .Skip(i + 1)
                .FirstOrDefault(s => AppliesToRunway(s, runway));

            if (nextItem is not null)
            {
                // Acceptance rate must be applied to flights, but not slots or runway mode changes
                var earliestTimeToTrailer = nextItem switch
                {
                    FlightSequenceItem nextFlightItem => nextFlightItem.Flight.LandingTime.Subtract(runway.AcceptanceRate),
                    _ => nextItem.Time
                };

                // TODO: Don't re-order flights here. That's not our job.

                // If the landing time is in conflict with the next item, we may need to move this flight behind it
                // if (landingTime.IsAfter(earliestTimeToTrailer))
                // {
                //     var isNewFlight = insertingFlights?.Contains(currentFlight.Callsign) ?? false;
                //     var canDelayNextItem = nextItem switch
                //     {
                //         // Slots and runway mode changes can't be delayed
                //         SlotSequenceItem => false,
                //         RunwayModeChangeSequenceItem => false,
                //
                //         // Manually-inserted flights can't be delayed
                //         // TODO: Verify this behaviour is correct
                //         FlightSequenceItem { Flight.IsManuallyInserted: true } => false,
                //
                //         // TODO: What about manual landing times?
                //
                //         // New flights can delay stable flights
                //         FlightSequenceItem { Flight.State: State.Stable } when isNewFlight => true,
                //
                //         // forceRescheduleStable allows stable and superstable flights to be delayed
                //         FlightSequenceItem { Flight.State: State.Stable or State.SuperStable } when isNewFlight || forceRescheduleStable => true,
                //
                //         // Unstable flights can always be delayed
                //         FlightSequenceItem { Flight.State: State.Unstable } => true,
                //
                //         _ => false
                //     };
                //
                //     // If we can't delay the next item, we need to move this flight behind it
                //     if (!canDelayNextItem)
                //     {
                //         // Find the actual index of the nextItem in the sequence
                //         var nextItemIndex = _sequence.IndexOf(nextItem);
                //         if (nextItemIndex == -1)
                //         {
                //             continue;
                //         }
                //
                //         (_sequence[i], _sequence[nextItemIndex]) = (_sequence[nextItemIndex], _sequence[i]);
                //
                //         // Decrement i to reprocess this index since we've moved something new into this position
                //         i--;
                //         continue;
                //     }
                // }
            }

            // TODO: Double check how this is supposed to work
            if (currentFlight.AircraftCategory == AircraftCategory.Jet && landingTime.IsAfter(currentFlight.LandingEstimate))
            {
                currentFlight.SetFlowControls(FlowControls.ReduceSpeed);
            }
            else
            {
                currentFlight.SetFlowControls(FlowControls.ProfileSpeed);
            }

            currentFlight.SetLandingTime(landingTime);

            // TODO: Don't change the runway here
            currentFlight.SetRunway(runwayIdentifier, currentFlight.RunwayManuallyAssigned);

            // TODO: Automatically look update STA_FF based on arrival config
            var arrivalInterval = _arrivalLookup.GetArrivalInterval(
                currentFlight.DestinationIdentifier,
                currentFlight.FeederFixIdentifier,
                currentFlight.AssignedArrivalIdentifier,
                currentFlight.AssignedRunwayIdentifier,
                currentFlight.AircraftType,
                currentFlight.AircraftCategory);
            if (arrivalInterval is not null)
            {
                var feederFixTime = currentFlight.LandingTime.Subtract(arrivalInterval.Value);
                currentFlight.SetFeederFixTime(feederFixTime);
            }

            currentFlight.ResetInitialEstimates();

            bool AppliesToRunway(ISequenceItem item, Runway runway)
            {
                string[] relevantRunways = [runway.Identifier, ..runway.Dependencies.Select(x => x.RunwayIdentifier).ToArray()];

                switch (item)
                {
                    case RunwayModeChangeSequenceItem:
                    case SlotSequenceItem slotItem when slotItem.Slot.RunwayIdentifiers.Any(r => relevantRunways.Contains(r)):
                    case FlightSequenceItem flightItem when relevantRunways.Contains(flightItem.Flight.AssignedRunwayIdentifier):
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}
