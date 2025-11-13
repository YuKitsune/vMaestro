using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public class Sequence
{
    readonly AirportConfiguration _airportConfiguration;

    readonly IArrivalLookup _arrivalLookup;
    readonly IClock _clock;

    readonly List<ISequenceItem> _sequence = [];

    public string AirportIdentifier { get; }
    public IReadOnlyList<Flight> Flights => _sequence.OfType<FlightSequenceItem>().Select(x => x.Flight).ToList().AsReadOnly();

    public Sequence(AirportConfiguration airportConfiguration, IArrivalLookup arrivalLookup, IClock clock)
    {
        _airportConfiguration = airportConfiguration;
        _arrivalLookup = arrivalLookup;
        _clock = clock;

        AirportIdentifier = airportConfiguration.Identifier;
        // CurrentRunwayMode = new RunwayMode(airportConfiguration.RunwayModes.First());
        _sequence.Add(
            new RunwayModeChangeSequenceItem(
                new RunwayMode(airportConfiguration.RunwayModes.First()),
                DateTimeOffset.MinValue,
                DateTimeOffset.MinValue));
    }

    public void ChangeRunwayMode(RunwayMode runwayMode)
    {
        var now = _clock.UtcNow();
        var index = IndexOf(now);
        InsertAt(
            index,
            new RunwayModeChangeSequenceItem(runwayMode, now, now));

        Schedule(index, forceRescheduleStable: true);
    }

    public void ChangeRunwayMode(
        RunwayMode runwayMode,
        DateTimeOffset lastLandingTimeForOldMode,
        DateTimeOffset firstLandingTimeForNewMode)
    {
        var index = IndexOf(lastLandingTimeForOldMode);
        InsertAt(index,
            new RunwayModeChangeSequenceItem(runwayMode, lastLandingTimeForOldMode, firstLandingTimeForNewMode));

        Schedule(index, forceRescheduleStable: true);
    }

    public RunwayMode GetRunwayModeAt(DateTimeOffset time)
    {
        var runwayModeItem = _sequence
            .OfType<RunwayModeChangeSequenceItem>()
            .LastOrDefault(i => i.Time <= time);
        if (runwayModeItem is null)
            throw new MaestroException("No runway mode found");

        return runwayModeItem.RunwayMode;
    }

    public RunwayMode GetRunwayModeAt(int index)
    {
        var runwayModeItem = _sequence
            .Take(index + 1)
            .OfType<RunwayModeChangeSequenceItem>()
            .LastOrDefault();
        if (runwayModeItem is null)
            throw new MaestroException("No runway mode found");

        return runwayModeItem.RunwayMode;
    }

    public void Insert(int index, Flight flight)
    {
        ValidateInsertionBetweenImmovableFlights(index, flight.AssignedRunwayIdentifier);
        InsertAt(index, new FlightSequenceItem(flight));
        Schedule(index, forceRescheduleStable: true);
    }

    public Flight? FindFlight(string callsign)
    {
        return _sequence
            .OfType<FlightSequenceItem>()
            .Select(i => i.Flight)
            .FirstOrDefault(f => f.Callsign == callsign);
    }

    public void Remove(Flight flight)
    {
        var index = _sequence.FindIndex(i => i is FlightSequenceItem f && f.Flight == flight);
        if (index == -1)
            throw new MaestroException($"{flight.Callsign} not found");

        _sequence.RemoveAt(index);
        Schedule(index, forceRescheduleStable: true);
    }

    public int FirstIndexOf(Func<Flight, bool> predicate)
    {
        return _sequence.FindIndex(i => i is FlightSequenceItem f && predicate(f.Flight));
    }

    public int FirstIndexOf(int startIndex, Func<Flight, bool> predicate)
    {
        return _sequence.FindIndex(startIndex, i => i is FlightSequenceItem f && predicate(f.Flight));
    }

    public int LastIndexOf(Func<Flight, bool> predicate)
    {
        return _sequence.FindLastIndex(i => i is FlightSequenceItem f && predicate(f.Flight));
    }

    public int LastIndexOf(int startIndex, Func<Flight, bool> predicate)
    {
        return _sequence.FindLastIndex(startIndex, i => i is FlightSequenceItem f && predicate(f.Flight));
    }

    public Guid CreateSlot(DateTimeOffset start, DateTimeOffset end, string[] runwayIdentifiers)
    {
        var id = Guid.NewGuid();
        var index = IndexOf(start);

        InsertAt(index, new SlotSequenceItem(new Slot(id, start, end, runwayIdentifiers)));
        Schedule(index, forceRescheduleStable: true);

        return id;
    }

    public void ModifySlot(Guid id, DateTimeOffset start, DateTimeOffset end)
    {
        var existingSlotItem = _sequence.OfType<SlotSequenceItem>().FirstOrDefault(s => s.Slot.Id == id);
        if (existingSlotItem is null)
            throw new MaestroException($"Slot {id} not found");

        var oldIndex = _sequence.IndexOf(existingSlotItem);
        _sequence.RemoveAt(oldIndex);

        var newSlotItem = new SlotSequenceItem(new Slot(id, start, end, existingSlotItem.Slot.RunwayIdentifiers));
        var newIndex = IndexOf(start);

        InsertAt(newIndex, newSlotItem);
        Schedule(newIndex, forceRescheduleStable: true);
    }

    public void DeleteSlot(Guid id)
    {
        var index = _sequence.FindIndex(s => s is SlotSequenceItem slotItem && slotItem.Slot.Id == id);
        if (index == -1)
            throw new MaestroException($"Slot {id} not found");

        _sequence.RemoveAt(index);

        Schedule(index, forceRescheduleStable: true);
    }

    public int NumberInSequence(Flight flight) => NumberInSequence(flight.Callsign);

    public int NumberForRunway(Flight flight) => NumberForRunway(flight.Callsign, flight.AssignedRunwayIdentifier);

    int NumberInSequence(string callsign)
    {
        // Get all flights (real and manually-inserted) that are not landed, ordered by landing time
        var allItems = _sequence
            .Where(i => i is FlightSequenceItem { Flight.State: not State.Landed })
            .OrderBy(i => i.Time)
            .ToList();

        var index = allItems.FindIndex(i =>
            i is FlightSequenceItem fsi && fsi.Flight.Callsign == callsign);

        if (index == -1)
            return -1;

        return index + 1;
    }

    int NumberForRunway(string callsign, string runwayIdentifier)
    {
        // Get all flights (real and manually-inserted) on the same runway that are not landed, ordered by landing time
        var allItems = _sequence
            .Where(i => i switch
            {
                FlightSequenceItem { Flight.State: not State.Landed } fs when fs.Flight.AssignedRunwayIdentifier == runwayIdentifier => true,
                _ => false
            })
            .OrderBy(i => i.Time)
            .ToList();

        var index = allItems.FindIndex(i =>
            i is FlightSequenceItem fsi && fsi.Flight.Callsign == callsign);
        return index + 1;
    }

    // TODO: Rename to snapshot
    public SequenceMessage ToMessage()
    {
        var sequencedFlights = _sequence
            .OfType<FlightSequenceItem>()
            .Select(i => i.Flight.ToMessage(this))
            .ToArray();

        var slots = _sequence
            .OfType<SlotSequenceItem>()
            .Select(x => x.Slot.ToMessage())
            .ToArray();

        var now = _clock.UtcNow();
        var currentRunwayMode = GetRunwayModeAt(now);
        var nextRunwayModeItem = _sequence.OfType<RunwayModeChangeSequenceItem>()
            .FirstOrDefault(i => i.Time > now);

        return new SequenceMessage
        {
            AirportIdentifier = AirportIdentifier,
            Flights = sequencedFlights,
            CurrentRunwayMode = currentRunwayMode.ToMessage(),
            NextRunwayMode = nextRunwayModeItem?.RunwayMode.ToMessage(),
            LastLandingTimeForCurrentMode = nextRunwayModeItem?.LastLandingTimeInPreviousMode ?? default,
            FirstLandingTimeForNextMode = nextRunwayModeItem?.FirstLandingTimeInNewMode ?? default,
            Slots = slots
        };
    }

    public void Restore(SequenceMessage message)
    {
        // Clear existing state
        _sequence.Clear();

        // Restore runway modes first (they need to be in the sequence for proper ordering)
        var currentRunwayMode = new RunwayMode(message.CurrentRunwayMode);
        _sequence.Add(new RunwayModeChangeSequenceItem(
            currentRunwayMode,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue));

        // If there's a next runway mode scheduled, add it
        if (message.NextRunwayMode is not null)
        {
            var nextRunwayMode = new RunwayMode(message.NextRunwayMode);
            _sequence.Add(new RunwayModeChangeSequenceItem(
                nextRunwayMode,
                message.LastLandingTimeForCurrentMode,
                message.FirstLandingTimeForNextMode));
        }

        // Restore slots
        foreach (var slotMessage in message.Slots)
        {
            var slot = new Slot(slotMessage.Id, slotMessage.StartTime, slotMessage.EndTime, slotMessage.RunwayIdentifiers);
            var index = IndexOf(slot.StartTime);
            InsertAt(index, new SlotSequenceItem(slot));
        }

        // Restore sequenced flights (both real and manually-inserted)
        foreach (var flightMessage in message.Flights)
        {
            var flight = new Flight(flightMessage);
            var index = IndexOf(flight.LandingTime);
            InsertAt(index, new FlightSequenceItem(flight));
        }
    }

    public void ThrowIfSlotIsUnavailable(int index, string runwayIdentifier)
    {
        ValidateInsertionBetweenImmovableFlights(index, runwayIdentifier);
    }

    // public void Recompute(Flight flight)
    // {
    //     var originalIndex = _sequence.FindIndex(i => i is FlightSequenceItem f && f.Flight == flight);
    //
    //     // TODO: What if the flight is already frozen?
    //     // TODO: If we recompute a flight that's in the frozen part of the sequence, should it get delayed until it's no longer in that part?
    //     // Validate BEFORE removing - exclude the flight being moved
    //     var newIndex = IndexOf(flight.LandingEstimate);
    //     if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
    //         ValidateInsertionBetweenImmovableFlights(newIndex, flight.AssignedRunwayIdentifier!);
    //
    //     // Remove and re-insert the flight by it's landing estimate
    //     _sequence.RemoveAll(i => i is FlightSequenceItem f && f.Flight == flight);
    //
    //     InsertAt(newIndex, new FlightSequenceItem(flight));
    //
    //     var recomputePoint = Math.Min(originalIndex, newIndex);
    //
    //     Schedule(recomputePoint, forceRescheduleStable: true);
    // }

    public void Move(Flight flight, int newIndex, bool forceRescheduleStable = false)
    {
        var currentIndex = IndexOf(flight);

        ValidateInsertionBetweenImmovableFlights(newIndex, flight.AssignedRunwayIdentifier);
        _sequence.RemoveAt(currentIndex);

        // Removing the flight will change the index of everything behind it
        if (newIndex > currentIndex)
            newIndex--;

        InsertAt(newIndex, new FlightSequenceItem(flight));

        var recomputePoint = Math.Min(newIndex, currentIndex);

        Schedule(recomputePoint, forceRescheduleStable);
    }

    public void Swap(Flight flight1, Flight flight2)
    {
        var index1 = IndexOf(flight1);
        if (index1 < 0)
            throw new MaestroException($"{flight1.Callsign} not found");

        var index2 = IndexOf(flight2);
        if (index2 < 0)
            throw new MaestroException($"{flight1.Callsign} not found");

        // Swap positions
        (_sequence[index1],  _sequence[index2]) = (_sequence[index2], _sequence[index1]);

        // Swap runways
        var runway1 = flight1.AssignedRunwayIdentifier;
        var runway2 = flight2.AssignedRunwayIdentifier;
        flight1.SetRunway(runway2, manual: true);
        flight1.SetRunway(runway1, manual: true);

        // Swap landing times
        var landingTime1 = flight1.LandingTime;
        var landingTime2 = flight2.LandingTime;
        flight1.SetLandingTime(landingTime2);
        flight1.SetLandingTime(landingTime1);

        // Fix feeder fix time
        var feederFixTime1 = GetFeederFixTime(flight1);
        if (feederFixTime1 is not null)
            flight1.SetFeederFixTime(feederFixTime1.Value);

        var feederFixTime2 = GetFeederFixTime(flight2);
        if (feederFixTime2 is not null)
            flight2.SetFeederFixTime(feederFixTime2.Value);

        // No need to re-schedule as we're exchanging two flights that are already scheduled
    }

    // TODO: Remove time parameter
    public void Reposition(Flight flight, DateTimeOffset _)
    {
        var existingFlight = _sequence.OfType<FlightSequenceItem>()
            .SingleOrDefault(f => f.Flight == flight);
        if (existingFlight is null)
            throw new MaestroException($"{flight.Callsign} not found in sequence");

        // Reposition by comparing ETA rather than STA
        var newIndex = IndexByEstimate(flight.LandingEstimate, flight.AssignedRunwayIdentifier);

        // Validate BEFORE removing - exclude the flight being moved
        if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
            ValidateInsertionBetweenImmovableFlights(newIndex, flight.AssignedRunwayIdentifier!);

        var oldIndex = _sequence.IndexOf(existingFlight);
        _sequence.RemoveAt(oldIndex);

        if (newIndex > oldIndex)
            newIndex--;

        InsertAt(newIndex, existingFlight);

        var reschedulePoint = Math.Min(oldIndex, newIndex);

        Schedule(reschedulePoint, forceRescheduleStable: false);
    }

    public void Reposition(Flight flight, RelativePosition relativePosition, string referenceCallsign)
    {
        var existingFlightItem = _sequence.OfType<FlightSequenceItem>()
            .SingleOrDefault(f => f.Flight == flight);
        if (existingFlightItem is null)
            throw new MaestroException($"{flight.Callsign} not found in sequence");

        var referenceFlightItem = _sequence.OfType<FlightSequenceItem>()
            .SingleOrDefault(f => f.Flight.Callsign == referenceCallsign);
        if (referenceFlightItem is null)
            throw new MaestroException($"{referenceCallsign} not found in sequence");

        var currentFlightIndex = _sequence.IndexOf(existingFlightItem);
        var referenceIndex = _sequence.IndexOf(referenceFlightItem);

        var insertionIndex = relativePosition switch
        {
            RelativePosition.Before => referenceIndex,
            RelativePosition.After => referenceIndex + 1,
            _ => throw new ArgumentOutOfRangeException()
        };

        if (!string.IsNullOrEmpty(referenceFlightItem.Flight.AssignedRunwayIdentifier))
            ValidateInsertionBetweenImmovableFlights(insertionIndex, referenceFlightItem.Flight.AssignedRunwayIdentifier!);

        // TODO: Account for runway mode changes
        existingFlightItem.Flight.SetRunway(referenceFlightItem.Flight.AssignedRunwayIdentifier!, manual: true);

        _sequence.Remove(existingFlightItem);
        if (currentFlightIndex >= insertionIndex)
            insertionIndex--;

        InsertAt(insertionIndex, existingFlightItem);

        var recomputeIndex = Math.Min(currentFlightIndex, insertionIndex);

        Schedule(recomputeIndex, forceRescheduleStable: true);
    }

    void InsertAt(int index, ISequenceItem item)
    {
        if (index >= _sequence.Count)
            _sequence.Add(item);
        else
            _sequence.Insert(index, item);
    }

    public void Schedule(int startIndex, bool forceRescheduleStable)
    {
        for (var i = startIndex; i < _sequence.Count; i++)
        {
            if (i == 0)
                continue;

            var currentItem = _sequence[i];
            if (currentItem is not FlightSequenceItem flightItem)
            {
                continue;
            }

            var currentFlight = flightItem.Flight;
            if (currentFlight.State is State.Landed or State.Frozen)
            {
                continue;
            }

            // Stable and SuperStable flights should not be rescheduled unless forced
            if (currentFlight.State is State.Stable or State.SuperStable && !forceRescheduleStable)
            {
                continue;
            }

            var runwayModeItem = _sequence
                .Take(i)
                .OfType<RunwayModeChangeSequenceItem>()
                .LastOrDefault();
            if (runwayModeItem is null)
                throw new Exception("No runway mode found");

            var currentRunwayMode = runwayModeItem.RunwayMode;

            // TODO: We need to do this in case we delay a flight into the next runway mode.
            // If we're delayed into a new mode, but the runway was manually assigned, no separation is applied to this
            // flight against other flights on the new mode, even though this flight is landing in the new mode.
            // Need to figure out how to handle this properly.

            // If the assigned runway isn't in the current mode, use the default
            Runway runway;
            if (currentFlight.FeederFixIdentifier is not null && !currentFlight.RunwayManuallyAssigned)
            {
                if (_airportConfiguration.PreferredRunways.TryGetValue(currentFlight.FeederFixIdentifier,
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

            // Determine the latest possible landing time based on the next item on this runway
            var nextItem = _sequence
                .Skip(i + 1)
                .FirstOrDefault(s => AppliesToRunway(s, runway));

            var latestLandingTime = nextItem switch
            {
                // Frozen and Landed flights cannot be delayed, other flights are fair game
                FlightSequenceItem { Flight.State: State.Frozen or State.Landed } frozenFlight => frozenFlight.Flight.LandingTime.Subtract(runway.AcceptanceRate),
                SlotSequenceItem slotItem => slotItem.Slot.StartTime,
                RunwayModeChangeSequenceItem runwayModeChange => runwayModeChange.LastLandingTimeInPreviousMode,
                _ => DateTime.MaxValue
            };

            // Determine the earliest possible landing time based on the preceding item on this runway
            var precedingItemsOnRunway = _sequence
                .Take(i)
                .Where(s => AppliesToRunway(s, runway))
                .ToList();

            var previousItem = precedingItemsOnRunway.Last();
            var earliestLandingTime = previousItem switch
            {
                FlightSequenceItem previousFlightItem => previousFlightItem.Flight.LandingTime.Add(runway.AcceptanceRate),
                SlotSequenceItem slotItem => slotItem.Slot.EndTime,
                RunwayModeChangeSequenceItem runwayModeChange => runwayModeChange.FirstLandingTimeInNewMode,
                _ => DateTime.MinValue
            };

            // If the previous item is a frozen flight, look ahead for any slots they could be occupying
            if (previousItem is FlightSequenceItem { Flight.State: State.Frozen })
            {
                var lastSlot = precedingItemsOnRunway
                    .OfType<SlotSequenceItem>()
                    .LastOrDefault();
                if (lastSlot is not null)
                {
                    earliestLandingTime = lastSlot.Slot.EndTime.IsAfter(earliestLandingTime)
                        ? lastSlot.Slot.EndTime
                        : earliestLandingTime;
                }
            }

            if (earliestLandingTime.IsAfter(latestLandingTime))
            {
                // TODO: There is no room in this slot, move the flight back and try again
            }

            var landingTime = currentFlight.LandingEstimate;

            // The flight behind this one can't be delayed, so we need to speed up
            if (landingTime.IsAfter(latestLandingTime))
            {
                landingTime = latestLandingTime;
            }

            // We're too close to whatever is in front of us, we need to delay
            if (landingTime.IsBefore(earliestLandingTime))
            {
                landingTime = earliestLandingTime;
            }

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
                    var previousItemIndex = _sequence.IndexOf(previousItem);
                    if (previousItemIndex != -1)
                    {
                        (_sequence[i], _sequence[previousItemIndex]) = (_sequence[previousItemIndex], _sequence[i]);
                        i = previousItemIndex - 1;
                        continue;
                    }
                }
            }

            // TODO: Do not swap with the next item if the maximum delay will be exceeded
            // If the next item in the sequence cannot be moved, move this flight behind it to ensure we don't conflict with it
            if (nextItem is not null)
            {
                var earliestTimeToTrailer = nextItem switch
                {
                    FlightSequenceItem nextFlightItem => nextFlightItem.Flight.LandingTime.Subtract(runway.AcceptanceRate),
                    _ => nextItem.Time
                };

                if (landingTime.IsAfter(earliestTimeToTrailer))
                {
                    var canDelayNextItem = nextItem switch
                    {
                        // Unstable flights can always be delayed
                        FlightSequenceItem { Flight.State: State.Unstable } => true,

                        // forceRescheduleStable allows stable and superstable flights to be delayed
                        FlightSequenceItem { Flight.State: State.Stable or State.SuperStable } when forceRescheduleStable => true,

                        _ => false
                    };

                    // If we can't delay the next item, we need to move this flight behind it
                    if (!canDelayNextItem)
                    {
                        // Find the actual index of the nextItem in the sequence
                        var nextItemIndex = _sequence.IndexOf(nextItem);
                        if (nextItemIndex == -1)
                        {
                            continue;
                        }

                        (_sequence[i], _sequence[nextItemIndex]) = (_sequence[nextItemIndex], _sequence[i]);

                        // Reprocess this index since we've moved something new into this position
                        i--;
                        continue;
                    }
                }
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

            Schedule(currentFlight, landingTime, runway.Identifier);

            // Preserve the order of the sequence with respect to flights on other runways
            if (i < _sequence.Count - 1)
            {
                var nextSequenceItem = _sequence[i + 1];
                if (!AppliesToRunway(nextSequenceItem, runway) && currentFlight.LandingTime.IsAfter(nextSequenceItem.Time))
                {
                    (_sequence[i], _sequence[i + 1]) = (_sequence[i + 1], _sequence[i]);
                    i--;
                }
            }

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

    public int IndexOf(DateTimeOffset dateTimeOffset)
    {
        for (var i = 0; i < _sequence.Count; i++)
        {
            if (dateTimeOffset > _sequence[i].Time)
                continue;

            return i;
        }

        return _sequence.Count;
    }

    int IndexByEstimate(DateTimeOffset landingEstimate, string runwayIdentifier)
    {
        for (var i = 0; i < _sequence.Count; i++)
        {
            var item = _sequence[i];
            if (item is not FlightSequenceItem flightItem || flightItem.Flight.AssignedRunwayIdentifier != runwayIdentifier)
                continue;

            if (landingEstimate > flightItem.Flight.LandingEstimate)
                continue;

            return i;
        }

        return _sequence.Count;
    }

    public int IndexOf(Flight flight)
    {
        return _sequence.FindIndex(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);
    }

    bool IsRunwayAffected(string runwayIdentifier, HashSet<string> affectedRunways)
    {
        // Direct match
        if (affectedRunways.Contains(runwayIdentifier))
            return true;

        // Check if any affected runway has dependencies on this runway
        var currentRunwayMode = GetRunwayModeAt(_clock.UtcNow());
        var thisRunway = currentRunwayMode.Runways.FirstOrDefault(r => r.Identifier == runwayIdentifier);
        if (thisRunway is null)
            return false;

        // Check if this runway depends on any affected runway
        foreach (var dependency in thisRunway.Dependencies)
        {
            if (affectedRunways.Contains(dependency.RunwayIdentifier))
                return true;
        }

        // Check if any affected runway depends on this runway
        foreach (var affectedRunwayId in affectedRunways)
        {
            var affectedRunway = currentRunwayMode.Runways.FirstOrDefault(r => r.Identifier == affectedRunwayId);
            if (affectedRunway?.Dependencies.Any(d => d.RunwayIdentifier == runwayIdentifier) == true)
                return true;
        }

        return false;
    }

    void ValidateInsertionBetweenImmovableFlights(int insertionIndex, string runwayIdentifier)
    {
        // Get the flights before and after the inserted item (excluding the inserted item itself)
        var previousFlight = GetPreviousFlightOnRunway();
        var nextFlight = GetNextFlightOnRunway();

        // Only validate if we're between two flights
        if (previousFlight == null || nextFlight == null)
            return;

        // Check if both are immovable (Frozen or manually inserted)
        // TODO: Trial ignoring manually inserted flights and allowing them to be delayed
        var isPreviousImmovable = previousFlight.State is State.Frozen or State.Landed || previousFlight.IsManuallyInserted;
        var isNextImmovable = nextFlight.State is State.Frozen or State.Landed || nextFlight.IsManuallyInserted;

        if (!isPreviousImmovable || !isNextImmovable)
            return;

        // Get runway acceptance rate
        var runwayMode = GetRunwayModeAt(nextFlight.LandingTime);
        var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == runwayIdentifier);
        if (runway == null)
            throw new MaestroException($"Runway {runwayIdentifier} not found in current runway mode");

        var minimumGap = runway.AcceptanceRate.Add(runway.AcceptanceRate); // 2x acceptance rate
        var actualGap = nextFlight.LandingTime - previousFlight.LandingTime;

        if (actualGap < minimumGap)
        {
            throw new MaestroException(
                $"Cannot insert flight on runway {runwayIdentifier} between frozen flights {previousFlight.Callsign} and {nextFlight.Callsign}. " +
                $"Gap of {actualGap.TotalMinutes:F1} minutes is less than minimum required separation of {minimumGap.TotalMinutes:F1} minutes.");
        }

        Flight? GetPreviousFlightOnRunway()
        {
            return _sequence
                .Take(insertionIndex)
                .OfType<FlightSequenceItem>()
                .Where(f => f.Flight.AssignedRunwayIdentifier == runwayIdentifier)
                .LastOrDefault()
                ?.Flight;
        }

        Flight? GetNextFlightOnRunway()
        {
            return _sequence
                .Skip(insertionIndex)
                .OfType<FlightSequenceItem>()
                .Where(f => f.Flight.AssignedRunwayIdentifier == runwayIdentifier)
                .FirstOrDefault()
                ?.Flight;
        }
    }

    void Schedule(Flight flight, DateTimeOffset landingTime, string runwayIdentifier)
    {
        flight.SetLandingTime(landingTime);
        flight.SetRunway(runwayIdentifier, manual: flight.RunwayManuallyAssigned);

        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && flight.FeederFixEstimate is not null && !flight.HasPassedFeederFix)
        {
            var arrivalInterval = _arrivalLookup.GetArrivalInterval(
                flight.DestinationIdentifier,
                flight.FeederFixIdentifier,
                flight.AssignedArrivalIdentifier,
                flight.AssignedRunwayIdentifier,
                flight.AircraftType,
                flight.AircraftCategory);
            if (arrivalInterval is not null)
            {
                var feederFixTime = flight.LandingTime.Subtract(arrivalInterval.Value);
                flight.SetFeederFixTime(feederFixTime);
            }
        }

        flight.ResetInitialEstimates();
    }

    DateTimeOffset? GetFeederFixTime(Flight flight)
    {
        var interval = _arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.AssignedArrivalIdentifier,
            flight.AssignedRunwayIdentifier,
            flight.AircraftType,
            flight.AircraftCategory);

        if (interval is null)
            return null;

        return flight.LandingTime.Subtract(interval.Value);
    }
}

public interface ISequenceItem
{
    DateTimeOffset Time { get; }
}

public interface IFlightSequenceItem : ISequenceItem
{
    State State { get; }
}

public record FlightSequenceItem(Flight Flight) : ISequenceItem
{
    // Always use LandingTime for sequence positioning to prevent discontinuities during state transitions
    public DateTimeOffset Time => Flight.LandingTime;
}

public record SlotSequenceItem(Slot Slot) : ISequenceItem
{
    public DateTimeOffset Time => Slot.StartTime;
}

public record RunwayModeChangeSequenceItem(
    RunwayMode RunwayMode,
    DateTimeOffset LastLandingTimeInPreviousMode,
    DateTimeOffset FirstLandingTimeInNewMode) : ISequenceItem
{
    public DateTimeOffset Time => LastLandingTimeInPreviousMode;
}
