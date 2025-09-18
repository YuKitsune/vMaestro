using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

// TODO Test cases:
// - Desequencing a flight, deallocates the slot and adds the flight to desequenced flights
// - AddPendingFlight adds a flight to pending flights
// - PurgeSlotsBefore removes slots before a given time and retains the 5 most recent landed flights
// - NumberInSequence returns the index of a flight in the entire sequence, starting from 1
// - NumberForRunway returns the index of a flight for its assigned runway, starting from 1

// TODO: Test cases:
// - Landed and Frozen flights are unaffected by runway modes and slots
// - Flights are not sequenced during slots
// - Flights are not sequenced during runway mode transitions
// - Landed, Frozen, SuperStable, and Stable flights are sequenced by ScheduledLandingTime
// - Unstable flights are sequenced by EstimatedLandingTime

public class Sequence
{
    readonly IArrivalLookup _arrivalLookup;
    readonly IPerformanceLookup _performanceLookup;
    readonly IClock _clock;

    private int _dummyCounter = 1;

    readonly List<Flight> _pendingFlights = [];
    readonly List<Flight> _deSequencedFlights = [];
    readonly List<ISequenceItem> _sequence = [];

    public string AirportIdentifier { get; }
    public IReadOnlyList<Flight> PendingFlights => _pendingFlights.AsReadOnly();
    public IReadOnlyList<Flight> DeSequencedFlights => _deSequencedFlights.AsReadOnly();
    public IReadOnlyList<Flight> Flights => _sequence.OfType<FlightSequenceItem>().Select(x => x.Flight).ToList().AsReadOnly();

    // public RunwayMode CurrentRunwayMode { get; private set; }
    // public DateTimeOffset LastLandingTimeForCurrentMode { get; private set; }
    // public RunwayMode? NextRunwayMode { get; private set; }
    // public DateTimeOffset FirstLandingTimeForNextMode { get; private set; }

    // public IReadOnlyList<Slot> Slots => _slots.AsReadOnly();

    public Sequence(Configuration.AirportConfiguration airportConfiguration, IArrivalLookup arrivalLookup, IPerformanceLookup performanceLookup, IClock clock)
    {
        _arrivalLookup = arrivalLookup;
        _performanceLookup = performanceLookup;
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
        var index = InsertByTime(new RunwayModeChangeSequenceItem(runwayMode, now, now), now, _sequence);
        Schedule(index, forceRescheduleStable: true);
    }

    public void ChangeRunwayMode(
        RunwayMode runwayMode,
        DateTimeOffset lastLandingTimeForOldMode,
        DateTimeOffset firstLandingTimeForNewMode)
    {
        var index = InsertByTime(
            new RunwayModeChangeSequenceItem(runwayMode, lastLandingTimeForOldMode, firstLandingTimeForNewMode),
            lastLandingTimeForOldMode,
            _sequence);

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

    public void AddDummyFlight(RelativePosition relativePosition, string referenceCallsign)
    {
        var referenceFlightItem = _sequence
            .OfType<FlightSequenceItem>()
            .FirstOrDefault(i => i.Flight.Callsign == referenceCallsign);
        if (referenceFlightItem is null)
            throw new MaestroException($"{referenceCallsign} not found");

        var dummyFlight = CreateDummyFlight(referenceFlightItem.Flight.AssignedRunwayIdentifier);
        var index = InsertRelative(dummyFlight, relativePosition, referenceCallsign);

        // TODO: The position in sequence is correct, but the landing time needs to be set

        dummyFlight.SetState(State.Frozen, _clock);

        Schedule(index, [referenceFlightItem.Flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    public void AddDummyFlight(DateTimeOffset landingTime, string[] runwayIdentifiers)
    {
        var runwayMode = GetRunwayModeAt(landingTime);

        // Find the best runway for the dummy flight
        var runway = runwayMode.Runways.FirstOrDefault(f => runwayIdentifiers.Contains(f.Identifier)) ??
                     runwayMode.Default;

        var dummyFlight = CreateDummyFlight(runway.Identifier);
        dummyFlight.SetLandingTime(landingTime);

        var index = InsertByTime(new FlightSequenceItem(dummyFlight), landingTime, _sequence);
        dummyFlight.SetState(State.Frozen, _clock);

        Schedule(index, [runway.Identifier], forceRescheduleStable: true);
    }

    Flight CreateDummyFlight(string runwayIdentifier)
    {
        var callsign = $"****{_dummyCounter++:00}*";
        var flight = new Flight(callsign, AirportIdentifier, DateTimeOffset.MinValue) // TODO: Need a new ctor
        {
            IsDummy = true
        };

        flight.SetRunway(runwayIdentifier, manual: true);
        return flight;
    }

    public void AddPendingFlight(Flight flight)
    {
        if (_pendingFlights.Any(f => f.Callsign == flight.Callsign))
            throw new MaestroException($"{flight.Callsign} is already in pending list");

        _pendingFlights.Add(flight);
    }

    public Flight? FindTrackedFlight(string callsign)
    {
        return _sequence.OfType<FlightSequenceItem>()
                   .Select(i => i.Flight)
                   .FirstOrDefault(f => f.Callsign == callsign)
               ?? _deSequencedFlights.FirstOrDefault(f => f.Callsign == callsign)
               ?? _pendingFlights.FirstOrDefault(f => f.Callsign == callsign);
    }

    public void Desequence(string callsign)
    {
        var flight = _sequence.OfType<FlightSequenceItem>()
            .Select(i => i.Flight)
            .FirstOrDefault(f => f.Callsign == callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found");

        var index = _sequence.FindIndex(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);
        if (index == -1)
            throw new MaestroException($"{callsign} not found");

        _sequence.RemoveAt(index);
        _deSequencedFlights.Add(flight);

        Schedule(index, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    public void Resume(string callsign)
    {
        var flight = _deSequencedFlights.FirstOrDefault(f => f.Callsign == callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found");
        _deSequencedFlights.Remove(flight);

        var index = InsertByTime(new FlightSequenceItem(flight), flight.LandingEstimate, _sequence);

        Schedule(index, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    // TODO: Reset the flight and place it into the pending list
    public void Remove(string callsign)
    {
        var flight = FindTrackedFlight(callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found in desequenced list");

        var index = _sequence.FindIndex(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);
        if (index == -1)
            throw new MaestroException($"{callsign} not found");

        _sequence.RemoveAt(index);

        Schedule(index, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    public void CreateSlot(DateTimeOffset start, DateTimeOffset end, string[] runwayIdentifiers)
    {
        var id = Guid.NewGuid();
        var index = InsertByTime(new SlotSequenceItem(new Slot(id, start, end, runwayIdentifiers)), start, _sequence);

        Schedule(index, runwayIdentifiers.ToHashSet(), forceRescheduleStable: true);
    }

    public void ModifySlot(Guid id, DateTimeOffset start, DateTimeOffset end)
    {
        var slotItem = _sequence.OfType<SlotSequenceItem>().FirstOrDefault(s => s.Slot.Id == id);
        if (slotItem is null)
            throw new MaestroException($"Slot {id} not found");

        slotItem.Slot.ChangeTime(start, end);

        // Reinsert at the correct position according to the start time
        _sequence.Remove(slotItem);
        var index = InsertByTime(slotItem, start, _sequence);

        Schedule(index, slotItem.Slot.RunwayIdentifiers.ToHashSet(), forceRescheduleStable: true);
    }

    public void DeleteSlot(Guid id)
    {
        var index = _sequence.FindIndex(s => s is SlotSequenceItem slotItem && slotItem.Slot.Id == id);
        if (index == -1)
            throw new MaestroException($"Slot {id} not found");

        var slotItem = (SlotSequenceItem)_sequence[index];
        _sequence.RemoveAt(index);

        Schedule(index, slotItem.Slot.RunwayIdentifiers.ToHashSet(), forceRescheduleStable: true);
    }

    public int NumberInSequence(Flight flight)
    {
        return _sequence
            .OfType<FlightSequenceItem>()
            .Select(i => i.Flight)
            .Where(f => f.State is not State.Landed)
            .OrderBy(f => f.LandingTime)
            .ToList()
            .IndexOf(flight) + 1;
    }

    public int NumberForRunway(Flight flight)
    {
        return _sequence
            .OfType<FlightSequenceItem>()
            .Select(i => i.Flight)
            .Where(f => f.State is not State.Landed)
            .Where(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier)
            .OrderBy(f => f.LandingTime)
            .ToList()
            .IndexOf(flight) + 1;
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
            PendingFlights = _pendingFlights.Select(f => f.ToMessage(this)).ToArray(),
            DeSequencedFlights = _deSequencedFlights.Select(f => f.ToMessage(this)).ToArray(),
            CurrentRunwayMode = currentRunwayMode.ToMessage(),
            NextRunwayMode = nextRunwayModeItem?.RunwayMode.ToMessage(),
            LastLandingTimeForCurrentMode = nextRunwayModeItem?.LastLandingTimeInPreviousMode ?? default,
            FirstLandingTimeForNextMode = nextRunwayModeItem?.FirstLandingTimeInNewMode ?? default,
            Slots = slots,
            DummyCounter = _dummyCounter
        };
    }

    // TODO: Re-implement
    public void Restore(SequenceMessage message)
    {
        // _trackedFlights.Clear();
        // _trackedFlights.AddRange(message.Flights.Select(f => new Flight(f)));
        //
        // _slots.Clear();
        // _slots.AddRange(message.Slots.Select(s => new Slot(s)));
        //
        // _dummyCounter = message.DummyCounter;
    }

    public void Insert(Flight newFlight, DateTimeOffset preferredTime)
    {
        var index = InsertByTime(new FlightSequenceItem(newFlight), preferredTime, _sequence);

        // TODO: Which runway?
        Schedule(index, forceRescheduleStable: true, insertingFlights: [newFlight.Callsign]);
    }

    public void Insert(Flight newFlight, RelativePosition relativePosition, string referenceCallsign)
    {
        var index = InsertRelative(newFlight, relativePosition, referenceCallsign);
        Schedule(index, forceRescheduleStable: true, insertingFlights: [newFlight.Callsign]);
    }

    int InsertRelative(Flight newFlight, RelativePosition relativePosition, string referenceCallsign)
    {
        var referenceFlightItem = _sequence
            .OfType<FlightSequenceItem>()
            .FirstOrDefault(i => i.Flight.Callsign == referenceCallsign);
        if (referenceFlightItem is null)
            throw new MaestroException(
                $"Reference flight {referenceCallsign} not found");

        var referenceIndex = _sequence.IndexOf(referenceFlightItem);
        if (referenceIndex == -1)
            throw new MaestroException("Reference flight not found in sequence");

        var insertionIndex = relativePosition switch
        {
            RelativePosition.Before => referenceIndex,
            RelativePosition.After => referenceIndex + 1,
            _ => throw new ArgumentOutOfRangeException()
        };

        // TODO: Prevent inserting before Frozen flights

        _sequence.Insert(insertionIndex, new FlightSequenceItem(newFlight));
        return insertionIndex;
    }

    public void Recompute(Flight flight)
    {
        var originalIndex = _sequence.FindIndex(i => i is FlightSequenceItem f && f.Flight == flight);

        // Remove and re-insert the flight by it's landing estimate
        _sequence.RemoveAll(i => i is FlightSequenceItem f && f.Flight == flight);
        InsertByTime(new FlightSequenceItem(flight), flight.LandingEstimate, _sequence);

        Schedule(originalIndex, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    public void MakePending(Flight flight)
    {
        flight.Reset();
        _pendingFlights.Add(flight);
        _sequence.RemoveAll(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);
    }

    public void Depart(Flight flight, DateTimeOffset takeOffTime)
    {
        _pendingFlights.Remove(flight);

        if (flight.EstimatedTimeEnroute is null)
            throw new MaestroException("Flight has no EET");

        // TODO: Combine with handler
        var targetTime = takeOffTime.Add(flight.EstimatedTimeEnroute.Value);
        flight.UpdateLandingEstimate(targetTime);

        var index = InsertByTime(new FlightSequenceItem(flight), targetTime, _sequence);
        Schedule(index, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true, insertingFlights: [flight.Callsign]);
    }

    public void MoveFlight(string callsign, DateTimeOffset newLandingTime, string[] runwayIdentifiers)
    {
        var flight = FindTrackedFlight(callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found");

        var runwayMode = GetRunwayModeAt(newLandingTime);
        var runway = runwayMode.Runways
            .FirstOrDefault(r => runwayIdentifiers.Contains(r.Identifier)) ?? runwayMode.Default;

        // TODO: Ensure flight cannot be inserted between two frozen flights where the separation is less than 2x the acceptance rate
        // TODO:

        // Remove flight from current position
        _sequence.RemoveAll(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);

        flight.SetRunway(runway.Identifier, manual: true);
        flight.SetLandingTime(newLandingTime, manual: true);

        // TODO: When moved to a position more than 1 runway separation from the preceding flight will, it should move forward towards this position to minimise the delay.
        var index = InsertByTime(new FlightSequenceItem(flight), newLandingTime, _sequence);

        Schedule(index, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    public void SwapFlights(string callsign1, string callsign2)
    {
        var flight1 = FindTrackedFlight(callsign1);
        if (flight1 is null)
            throw new MaestroException($"{callsign1} not found");

        var flight2 = FindTrackedFlight(callsign2);
        if (flight2 is null)
            throw new MaestroException($"{callsign2} not found");

        var flight1Index = _sequence.FindIndex(i => i is FlightSequenceItem f && f.Flight == flight1);
        if (flight1Index == -1)
            throw new MaestroException($"{callsign1} not found in sequence");

        var flight2Index = _sequence.FindIndex(i => i is FlightSequenceItem f && f.Flight == flight2);
        if (flight2Index == -1)
            throw new MaestroException($"{callsign2} not found in sequence");

        // Swap the flights in the sequence
        _sequence[flight1Index] = new FlightSequenceItem(flight2);
        _sequence[flight2Index] = new FlightSequenceItem(flight1);

        // Swap the landing times
        var tempLandingTime = flight1.LandingTime;
        var tempFeederFixTime = flight1.FeederFixTime;

        flight1.SetLandingTime(flight2.LandingTime, manual: true);
        if (flight2.FeederFixTime is not null)
            flight1.SetFeederFixTime(flight2.FeederFixTime.Value);

        flight2.SetLandingTime(tempLandingTime, manual: true);
        if (tempFeederFixTime is not null)
            flight2.SetFeederFixTime(tempFeederFixTime.Value);
    }

    public void Reposition(Flight flight, DateTimeOffset time)
    {
        // Remove and re-insert the flight at the specified time
        _sequence.RemoveAll(i => i is FlightSequenceItem f && f.Flight == flight);
        var index = InsertByTime(new FlightSequenceItem(flight), time, _sequence);

        Schedule(index, [flight.AssignedRunwayIdentifier]);
    }

    void Schedule(
        int startIndex,
        HashSet<string>? affectedRunways = null,
        bool forceRescheduleStable = false,
        HashSet<string>? insertingFlights = null)
    {
        for (var i = startIndex; i < _sequence.Count; i++)
        {
            if (i == 0)
                continue;

            var currentItem = _sequence[i];
            if (currentItem is not FlightSequenceItem flightItem)
                continue;

            var currentFlight = flightItem.Flight;
            if (currentFlight.State is State.Landed or State.Frozen)
                continue;

            // Stable and SuperStable flights should not have their landing times changed
            // unless we're forcing a reschedule due to operational changes
            if (currentFlight.State is State.Stable or State.SuperStable && !forceRescheduleStable)
                continue;

            // Skip flights on unaffected runways if runway filter is specified
            if (affectedRunways is not null &&
                !string.IsNullOrEmpty(currentFlight.AssignedRunwayIdentifier) &&
                !IsRunwayAffected(currentFlight.AssignedRunwayIdentifier, affectedRunways))
                continue;

            var runwayModeItem = _sequence
                .Take(i - 1)
                .OfType<RunwayModeChangeSequenceItem>()
                .LastOrDefault();
            if (runwayModeItem is null)
                throw new Exception("No runway mode found");

            var currentRunwayMode = runwayModeItem.RunwayMode;

            // If the assigned runway isn't in the current mode, use the default
            // TODO: Need to use the preferred runway for the FF
            var runway = currentRunwayMode.Runways
                             .FirstOrDefault(r => r.Identifier == currentFlight.AssignedRunwayIdentifier)
                         ?? currentRunwayMode.Default;

            var precedingItemsOnRunway = _sequence
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

            // If the previous flight is frozen, look ahead for any slots they may be occupying
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

            var earliestAllowedTime = currentFlight.LandingEstimate;

            // Respect manually assigned landing times as the absolute earliest time
            if (currentFlight.ManualLandingTime)
            {
                earliestAllowedTime = currentFlight.LandingTime;
            }

            var landingTime = earliestLandingTimeFromPrevious.IsAfter(earliestAllowedTime)
                ? earliestLandingTimeFromPrevious
                : earliestAllowedTime;

            // Check if this landing time would conflict with the next item in the sequence
            var nextItem = _sequence
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

                if (landingTime.IsSameOrAfter(earliestTimeToTrailer))
                {
                    // New flights can displace stable flights
                    var currentFlightIsBeingInserted = insertingFlights?.Contains(currentFlight.Callsign) ?? false;

                    var isDisplacingStableFlight = nextItem is FlightSequenceItem { Flight.State: State.Stable or State.SuperStable };
                    var isConflictingWithSlotOrRunwayChange = nextItem is not FlightSequenceItem;

                    // If the item behind this one is NOT an unstable flight, swap their positions and try scheduling
                    // again on the next iteration.
                    // If it IS an unstable flight, we'll just continue and schedule it on the next iteration.
                    // This is to prevent unstable flights from displacing stable ones.
                    if ((!currentFlightIsBeingInserted && isDisplacingStableFlight) || isConflictingWithSlotOrRunwayChange)
                    {
                        _sequence[i] = nextItem;
                        _sequence[i + 1] = currentItem;
                        continue;
                    }
                }
            }

            // TODO: Double check how this is supposed to work
            var performance = _performanceLookup.GetPerformanceDataFor(currentFlight.AircraftType);
            if (performance is not null && performance.AircraftCategory == AircraftCategory.Jet && landingTime.IsAfter(currentFlight.LandingEstimate))
            {
                currentFlight.SetFlowControls(FlowControls.ReduceSpeed);
            }
            else
            {
                currentFlight.SetFlowControls(FlowControls.ProfileSpeed);
            }

            Schedule(currentFlight, landingTime, runway.Identifier, performance);

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

    int InsertByTime(ISequenceItem item, DateTimeOffset dateTimeOffset, List<ISequenceItem> sequence)
    {
        if (item is not FlightSequenceItem flightItem)
        {
            // Non-flight items use simple time-based insertion
            for (int i = 0; i < sequence.Count; i++)
            {
                if (dateTimeOffset >= sequence[i].Time)
                    continue;

                sequence.Insert(i, item);
                return i;
            }

            sequence.Add(item);
            return sequence.Count - 1;
        }

        // For flights, avoid inserting between slot start/end for same runway
        for (int i = 0; i < sequence.Count; i++)
        {
            if (dateTimeOffset > sequence[i].Time)
                continue;

            // Check if we're trying to insert between a slot start and end for this runway
            if (IsInsertionBlockedBySlot(i, flightItem.Flight.AssignedRunwayIdentifier, sequence))
                continue;

            // Check if we're trying to insert before a protected flight
            var currentItemAtPosition = sequence[i];
            if (currentItemAtPosition is FlightSequenceItem currentFlightItem)
            {
                var currentFlightState = currentFlightItem.Flight.State;

                // New flights cannot be inserted before SuperStable, Frozen, or Landed flights
                if (currentFlightState is State.SuperStable or State.Frozen or State.Landed)
                {
                    continue; // Skip this position and look for the next one
                }
            }

            sequence.Insert(i, item);
            return i;
        }

        sequence.Add(item);
        return sequence.Count - 1;
    }

    int InsertNewFlightByTime(FlightSequenceItem flightItem, DateTimeOffset preferredTime, List<ISequenceItem> sequence)
    {
        // New flights cannot be inserted before SuperStable, Frozen, or Landed flights
        var lastFixedFlightIndex = sequence.FindLastIndex(i =>
            i is FlightSequenceItem { Flight.State: State.SuperStable or State.Frozen or State.Landed });
        var start = Math.Max(0, lastFixedFlightIndex + 1);

        for (var i = start; i < sequence.Count; i++)
        {
            if (preferredTime >= sequence[i].Time)
                continue;

            // Check if we're trying to insert between a slot start and end for this runway
            if (IsInsertionBlockedBySlot(i, flightItem.Flight.AssignedRunwayIdentifier, sequence))
                continue;

            sequence.Insert(i, flightItem);
            return i;
        }

        sequence.Add(flightItem);
        return sequence.Count - 1;
    }

    bool IsInsertionBlockedBySlot(int insertionIndex, string? runwayIdentifier, List<ISequenceItem> sequence)
    {
        if (string.IsNullOrEmpty(runwayIdentifier))
            return false;

        var insertionTime = insertionIndex < sequence.Count ? sequence[insertionIndex].Time : DateTimeOffset.MaxValue;

        // Check if insertion time falls within any slot for this runway
        foreach (var item in sequence.Take(insertionIndex))
        {
            if (item is SlotSequenceItem slotItem &&
                slotItem.Slot.RunwayIdentifiers.Contains(runwayIdentifier) &&
                insertionTime >= slotItem.Slot.StartTime &&
                insertionTime < slotItem.Slot.EndTime)
            {
                return true;
            }
        }

        // Check for runway mode transition gaps
        var runwayModeItems = sequence.Take(insertionIndex)
            .OfType<RunwayModeChangeSequenceItem>()
            .OrderBy(rm => rm.Time)
            .ToList();

        for (int i = 0; i < runwayModeItems.Count; i++)
        {
            var currentMode = runwayModeItems[i];
            var nextMode = i + 1 < runwayModeItems.Count ? runwayModeItems[i + 1] : null;

            if (nextMode is not null &&
                currentMode.RunwayMode.Runways.Any(r => r.Identifier == runwayIdentifier) &&
                insertionTime > currentMode.LastLandingTimeInPreviousMode &&
                insertionTime < nextMode.FirstLandingTimeInNewMode)
            {
                return true;
            }
        }

        return false;
    }

    (DateTimeOffset LandingTime, Runway Runway) FindOptimalRunwayAndTime(
        RunwayMode runwayMode,
        DateTimeOffset preferredTime,
        IEnumerable<ISequenceItem> precedingItems,
        Flight? flight = null)
    {
        var bestLandingTime = preferredTime;
        var bestRunway = runwayMode.Default;

        foreach (var runway in runwayMode.Runways)
        {
            var earliestLandingTime = CalculateEarliestLandingTimeForRunway(
                runway,
                preferredTime,
                precedingItems,
                flight);

            if (bestLandingTime < earliestLandingTime)
            {
                bestLandingTime = earliestLandingTime;
                bestRunway = runway;
            }
        }

        return (bestLandingTime, bestRunway);
    }

    DateTimeOffset CalculateEarliestLandingTimeForRunway(
        Runway runway,
        DateTimeOffset preferredTime,
        IEnumerable<ISequenceItem> precedingItems,
        Flight? flight = null)
    {
        var earliestTime = preferredTime; // Flight cannot land earlier than its ETA

        // Find the most recent preceding flight on this runway
        var lastFlightOnRunway = precedingItems
            .OfType<FlightSequenceItem>()
            .Where(f => f.Flight.AssignedRunwayIdentifier == runway.Identifier)
            .LastOrDefault();

        if (lastFlightOnRunway is not null)
        {
            var leaderLandingTime = lastFlightOnRunway.Flight.LandingTime;
            var requiredSeparationTime = leaderLandingTime.Add(runway.AcceptanceRate);

            // If leader lands after our preferred time, we must be delayed behind them
            if (leaderLandingTime >= preferredTime)
            {
                earliestTime = requiredSeparationTime;
            }
            // If leader lands before our preferred time but within acceptance rate window
            else if (requiredSeparationTime > preferredTime)
            {
                earliestTime = requiredSeparationTime;
            }
            // Otherwise, leader is far enough ahead that we can land at our preferred time
        }

        // Check for slot conflicts on this runway (frozen flights are exempt)
        if (flight?.State != State.Frozen)
        {
            var conflictingSlots = precedingItems
                .OfType<SlotSequenceItem>()
                .Where(slot => slot.Slot.RunwayIdentifiers.Contains(runway.Identifier))
                .Where(slot => earliestTime >= slot.Slot.StartTime && earliestTime < slot.Slot.EndTime);

            foreach (var slotItem in conflictingSlots)
            {
                // If we're in a slot, delay until after the slot ends
                if (slotItem.Slot.EndTime > earliestTime)
                    earliestTime = slotItem.Slot.EndTime;
            }
        }

        return earliestTime;
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

    void Schedule(Flight flight, DateTimeOffset landingTime, string runwayIdentifier, AircraftPerformanceData performanceData)
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
                performanceData);
            if (arrivalInterval is not null)
            {
                var feederFixTime = flight.LandingTime.Subtract(arrivalInterval.Value);
                flight.SetFeederFixTime(feederFixTime);
            }
            else
            {
                // _logger.Warning("Could not update feeder fix time for {Callsign}, no arrival interval found", flight.Callsign);
            }
        }

        flight.ResetInitialEstimates();
    }

    interface ISequenceItem
    {
        DateTimeOffset Time { get; }
    }

    record FlightSequenceItem(Flight Flight) : ISequenceItem
    {
        public DateTimeOffset Time => Flight.State == State.Unstable ? Flight.LandingEstimate : Flight.LandingTime;
    }

    record SlotSequenceItem(Slot Slot) : ISequenceItem
    {
        public DateTimeOffset Time => Slot.StartTime;
    }

    record RunwayModeChangeSequenceItem(
        RunwayMode RunwayMode,
        DateTimeOffset LastLandingTimeInPreviousMode,
        DateTimeOffset FirstLandingTimeInNewMode) : ISequenceItem
    {
        public DateTimeOffset Time => LastLandingTimeInPreviousMode;
    }
}
