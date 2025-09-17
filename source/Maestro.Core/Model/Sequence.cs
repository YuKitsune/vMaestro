using Maestro.Core.Configuration;
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
    readonly AirportConfiguration _airportConfiguration;

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
        _airportConfiguration = airportConfiguration;
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
        Serilog.Log.Debug("ChangeRunwayMode: {RunwayMode} inserted at {Time} (index {Index})", runwayMode.Identifier, now, index);
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
        Serilog.Log.Debug("ChangeRunwayMode: {RunwayMode} inserted at {Time} with a first landing time of {FirstLandingTime} (index {Index})", runwayMode.Identifier, lastLandingTimeForOldMode, firstLandingTimeForNewMode, index);

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
        // Check for duplicate flights - prevent the same flight from being inserted multiple times
        var existingFlight = _sequence.OfType<FlightSequenceItem>()
            .FirstOrDefault(f => f.Flight.Callsign == newFlight.Callsign);

        if (existingFlight is not null)
        {
            throw new MaestroException($"Flight {newFlight.Callsign} already exists in sequence at position {_sequence.IndexOf(existingFlight)}.");
        }

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
        var newIndex = InsertByTime(new FlightSequenceItem(flight), flight.LandingEstimate, _sequence);

        var recomputePoint = Math.Min(originalIndex, newIndex);

        Serilog.Log.Debug("Recompute: Flight {Callsign} moved from {OriginalIndex} to {NewIndex}, recompute point: {RecomputePoint}", flight.Callsign, originalIndex, newIndex, recomputePoint);

        Schedule(recomputePoint, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
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
        var existingFlight = _sequence.OfType<FlightSequenceItem>()
            .SingleOrDefault(f => f.Flight == flight);
        if (existingFlight is null)
            throw new MaestroException($"{flight.Callsign} not found in sequence");

        var oldIndex = _sequence.IndexOf(existingFlight);
        _sequence.RemoveAt(oldIndex);

        // Remove and re-insert the flight at the specified time
        _sequence.RemoveAll(i => i is FlightSequenceItem f && f.Flight == flight);
        var index = InsertByTime(new FlightSequenceItem(flight), time, _sequence);

        var reschedulePoint = Math.Min(oldIndex, index);

        Serilog.Log.Debug("Reposition: Flight {Callsign} moved from {OldIndex} to {NewIndex}. Reschedule from {ReschedulePoint}",
            flight.Callsign, oldIndex, index, reschedulePoint);

        Schedule(reschedulePoint, [flight.AssignedRunwayIdentifier]);
    }

    void Schedule(
        int startIndex,
        HashSet<string>? affectedRunways = null,
        bool forceRescheduleStable = false,
        HashSet<string>? insertingFlights = null)
    {
        Serilog.Log.Debug("Schedule: Starting from index {StartIndex}, affectedRunways: {AffectedRunways}, forceRescheduleStable: {ForceRescheduleStable}, insertingFlights: {InsertingFlights}",
            startIndex, affectedRunways != null ? string.Join(",", affectedRunways) : "null", forceRescheduleStable, insertingFlights != null ? string.Join(",", insertingFlights) : "null");

        for (var i = startIndex; i < _sequence.Count; i++)
        {
            if (i == 0)
                continue;

            var currentItem = _sequence[i];
            if (currentItem is not FlightSequenceItem flightItem)
            {
                Serilog.Log.Debug("Schedule: Index {Index} is not a flight item ({ItemType}), skipping", i, currentItem.GetType().Name);
                continue;
            }

            var currentFlight = flightItem.Flight;
            Serilog.Log.Debug("Schedule: Processing flight {Callsign} at index {Index}, state: {State}, runway: {Runway}, estimate: {Estimate}, current landing time: {LandingTime}",
                currentFlight.Callsign, i, currentFlight.State, currentFlight.AssignedRunwayIdentifier, currentFlight.LandingEstimate, currentFlight.LandingTime);

            if (currentFlight.State is State.Landed or State.Frozen)
            {
                Serilog.Log.Debug("Schedule: Flight {Callsign} is {State}, skipping", currentFlight.Callsign, currentFlight.State);
                continue;
            }

            // Stable and SuperStable flights should not have their landing times changed
            // unless we're forcing a reschedule due to operational changes
            if (currentFlight.State is State.Stable or State.SuperStable && !forceRescheduleStable)
            {
                Serilog.Log.Debug("Schedule: Flight {Callsign} is {State} and forceRescheduleStable is false, skipping", currentFlight.Callsign, currentFlight.State);
                continue;
            }

            // Skip flights on unaffected runways if runway filter is specified
            if (affectedRunways is not null &&
                !string.IsNullOrEmpty(currentFlight.AssignedRunwayIdentifier) &&
                !IsRunwayAffected(currentFlight.AssignedRunwayIdentifier, affectedRunways))
            {
                Serilog.Log.Debug("Schedule: Flight {Callsign} on runway {Runway} is not affected by runway filter, skipping", currentFlight.Callsign, currentFlight.AssignedRunwayIdentifier);
                continue;
            }

            var runwayModeItem = _sequence
                .Take(i)
                .OfType<RunwayModeChangeSequenceItem>()
                .LastOrDefault();
            if (runwayModeItem is null)
                throw new Exception("No runway mode found");

            var currentRunwayMode = runwayModeItem.RunwayMode;
            Serilog.Log.Debug("Schedule: Flight {Callsign} using runway mode: {RunwayMode}", currentFlight.Callsign, currentRunwayMode.Identifier);

            // If the assigned runway isn't in the current mode, use the default
            // TODO: Need to use the preferred runway for the FF
            Runway runway;
            if (currentFlight.FeederFixIdentifier is not null && !currentFlight.RunwayManuallyAssigned)
            {
                if (_airportConfiguration.PreferredRunways.TryGetValue(currentFlight.FeederFixIdentifier,
                        out var preferredRunways))
                {
                    runway = currentRunwayMode.Runways
                                 .FirstOrDefault(r => preferredRunways.Contains(r.Identifier))
                             ?? currentRunwayMode.Default;
                    Serilog.Log.Debug("Schedule: Flight {Callsign} assigned runway {Runway} based on feeder fix {FeederFix} preferences",
                        currentFlight.Callsign, runway.Identifier, currentFlight.FeederFixIdentifier);
                }
                else
                {
                    runway = currentRunwayMode.Default;
                    Serilog.Log.Debug("Schedule: Flight {Callsign} assigned default runway {Runway} (no preferences for feeder fix {FeederFix})",
                        currentFlight.Callsign, runway.Identifier, currentFlight.FeederFixIdentifier);
                }
            }
            else
            {
                runway = currentRunwayMode.Runways
                             .FirstOrDefault(r => r.Identifier == currentFlight.AssignedRunwayIdentifier)
                         ?? currentRunwayMode.Default;
                Serilog.Log.Debug("Schedule: Flight {Callsign} using assigned/default runway {Runway}",
                    currentFlight.Callsign, runway.Identifier);
            }

            // Determine the earliest possible landing time based on the preceding item on this runway
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

            Serilog.Log.Debug("Schedule: Flight {Callsign} previous item type: {PreviousItemType}, earliest time from previous: {EarliestTime}",
                currentFlight.Callsign, previousItem.GetType().Name, earliestLandingTimeFromPrevious);

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

            var earliestAllowedTime = currentFlight.ManualLandingTime
                ? currentFlight.LandingTime
                : currentFlight.LandingEstimate;

            Serilog.Log.Debug("Schedule: Flight {Callsign} earliest allowed time: {EarliestAllowed} (manual: {IsManual})",
                currentFlight.Callsign, earliestAllowedTime, currentFlight.ManualLandingTime);

            // Ensure the flight isn't sped up to meet the previous item
            var landingTime = earliestLandingTimeFromPrevious.IsAfter(earliestAllowedTime)
                ? earliestLandingTimeFromPrevious
                : earliestAllowedTime;

            Serilog.Log.Debug("Schedule: Flight {Callsign} calculated landing time: {LandingTime} (from previous: {FromPrevious}, from allowed: {FromAllowed})",
                currentFlight.Callsign, landingTime, earliestLandingTimeFromPrevious, earliestAllowedTime);

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

                Serilog.Log.Debug("Schedule: Flight {Callsign} next item type: {NextItemType}, earliest time to trailer: {EarliestTimeToTrailer}",
                    currentFlight.Callsign, nextItem.GetType().Name, earliestTimeToTrailer);

                // If the landing time is in conflict with the next item, we may need to move this flight behind it
                if (landingTime.IsAfter(earliestTimeToTrailer))
                {
                    // The only times we can displace (i.e. delay) a flight behind us:
                    // 1. We're rescheduling stable flights
                    // 2. The flight behind us is unstable
                    // 3. The current flight is being inserted (i.e. not already in the sequence) and the flight behind us is either stable or unstable

                    var isNewFlight = insertingFlights?.Contains(currentFlight.Callsign) ?? false;
                    var canDisplace = nextItem is FlightSequenceItem nextFlight &&
                                      (forceRescheduleStable ||
                                       nextFlight.Flight.State is State.Unstable ||
                                       (isNewFlight && nextFlight.Flight.State is State.Unstable or State.Stable));

                    Serilog.Log.Debug("Schedule: Flight {Callsign} conflicts with next item, isNewFlight: {IsNewFlight}, canDisplace: {CanDisplace}",
                        currentFlight.Callsign, isNewFlight, canDisplace);

                    // If we can't displace (i.e. delay) the next item, we need to move this flight behind it
                    if (!canDisplace)
                    {
                        Serilog.Log.Debug("Schedule: Swapping flight {Callsign} with next item, will reprocess at index {Index}",
                            currentFlight.Callsign, i);
                        _sequence[i] = nextItem;
                        _sequence[i + 1] = currentItem;

                        // If we've swapped positions with another flight, re-process this index so that we don't skip it
                        if (nextItem is FlightSequenceItem)
                            i--;

                        // No need to schedule this flight as we'll pick it up again in i + 2 iteration
                        continue;
                    }
                    else
                    {
                        Serilog.Log.Debug("Schedule: Flight {Callsign} can displace next item, continuing with current landing time",
                            currentFlight.Callsign);
                    }
                }
                else
                {
                    Serilog.Log.Debug("Schedule: Flight {Callsign} has no conflict with next item", currentFlight.Callsign);
                }
            }
            else
            {
                Serilog.Log.Debug("Schedule: Flight {Callsign} has no next item on runway", currentFlight.Callsign);
            }

            // TODO: Double check how this is supposed to work
            var performance = _performanceLookup.GetPerformanceDataFor(currentFlight.AircraftType);
            if (performance is not null && performance.AircraftCategory == AircraftCategory.Jet && landingTime.IsAfter(currentFlight.LandingEstimate))
            {
                currentFlight.SetFlowControls(FlowControls.ReduceSpeed);
                Serilog.Log.Debug("Schedule: Flight {Callsign} flow controls set to ReduceSpeed (delayed)", currentFlight.Callsign);
            }
            else
            {
                currentFlight.SetFlowControls(FlowControls.ProfileSpeed);
                Serilog.Log.Debug("Schedule: Flight {Callsign} flow controls set to ProfileSpeed", currentFlight.Callsign);
            }

            var originalLandingTime = currentFlight.LandingTime;
            Schedule(currentFlight, landingTime, runway.Identifier, performance);

            Serilog.Log.Information("Schedule: Flight {Callsign} scheduled - Index: {Index}, Runway: {Runway}, Original time: {OriginalTime}, New time: {NewTime}, State: {State}",
                currentFlight.Callsign, i, runway.Identifier, originalLandingTime, currentFlight.LandingTime, currentFlight.State);

            // Immediate validation to prevent any temporary overlaps
            ValidateNoOverlapsOnRunway(runway.Identifier, currentFlight);

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

        // Log any separation violations or forbidden period violations
        LogSequenceViolations();
    }

    int InsertByTime(ISequenceItem item, DateTimeOffset dateTimeOffset, List<ISequenceItem> sequence)
    {
        // Check for duplicate flights - prevent the same flight from being inserted multiple times
        if (item is FlightSequenceItem flightItem)
        {
            var existingFlight = sequence.OfType<FlightSequenceItem>()
                .FirstOrDefault(f => f.Flight.Callsign == flightItem.Flight.Callsign);

            if (existingFlight is not null)
            {
                throw new MaestroException($"Flight {flightItem.Flight.Callsign} already exists in sequence at position {sequence.IndexOf(existingFlight)}.");
            }
        }

        for (var i = 0; i < sequence.Count; i++)
        {
            if (dateTimeOffset > sequence[i].Time)
                continue;

            sequence.Insert(i, item);
            return i;
        }

        sequence.Add(item);
        return sequence.Count - 1;
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

    void LogSequenceViolations()
    {
        // Group flights by runway for separation checking
        var flightsByRunway = _sequence.OfType<FlightSequenceItem>()
            .Where(f => !string.IsNullOrEmpty(f.Flight.AssignedRunwayIdentifier))
            .GroupBy(f => f.Flight.AssignedRunwayIdentifier);

        foreach (var runwayGroup in flightsByRunway)
        {
            var runwayId = runwayGroup.Key;
            var flightsOnRunway = runwayGroup.OrderBy(f => f.Flight.LandingTime).ToList();

            // Get runway configuration for acceptance rate
            var currentRunwayMode = GetRunwayModeAt(_clock.UtcNow());
            var runway = currentRunwayMode.Runways.FirstOrDefault(r => r.Identifier == runwayId) ?? currentRunwayMode.Default;

            // Check separation violations between consecutive flights
            for (int i = 1; i < flightsOnRunway.Count; i++)
            {
                var previousFlight = flightsOnRunway[i - 1].Flight;
                var currentFlight = flightsOnRunway[i].Flight;

                var actualSeparation = currentFlight.LandingTime - previousFlight.LandingTime;
                if (actualSeparation < runway.AcceptanceRate)
                {
                    Serilog.Log.Warning("Insufficient separation on runway {Runway}: {PreviousFlight} at {PreviousTime}, {CurrentFlight} at {CurrentTime}, separation {ActualSeparation} < required {RequiredSeparation}",
                        runwayId, previousFlight.Callsign, previousFlight.LandingTime,
                        currentFlight.Callsign, currentFlight.LandingTime,
                        actualSeparation, runway.AcceptanceRate);
                }

                // Check if flights have exactly the same landing time
                if (actualSeparation == TimeSpan.Zero)
                {
                    Serilog.Log.Error("CRITICAL: Multiple flights scheduled at same time on runway {Runway}: {Flight1} and {Flight2} both at {Time}",
                        runwayId, previousFlight.Callsign, currentFlight.Callsign, currentFlight.LandingTime);
                }
            }

            // Check for flights landing during runway change periods
            var runwayModeChanges = _sequence.OfType<RunwayModeChangeSequenceItem>().ToList();
            foreach (var change in runwayModeChanges)
            {
                var flightsDuringChange = flightsOnRunway
                    .Where(f => f.Flight.LandingTime > change.LastLandingTimeInPreviousMode &&
                               f.Flight.LandingTime < change.FirstLandingTimeInNewMode)
                    .ToList();

                foreach (var flightItem in flightsDuringChange)
                {
                    Serilog.Log.Warning("Flight {Callsign} scheduled during runway change period on {Runway}: landing at {LandingTime}, change period {StartTime} to {EndTime}",
                        flightItem.Flight.Callsign, runwayId, flightItem.Flight.LandingTime,
                        change.LastLandingTimeInPreviousMode, change.FirstLandingTimeInNewMode);
                }
            }

            // Check for flights landing during slots
            var slots = _sequence.OfType<SlotSequenceItem>()
                .Where(s => s.Slot.RunwayIdentifiers.Contains(runwayId))
                .ToList();

            foreach (var slot in slots)
            {
                var flightsDuringSlot = flightsOnRunway
                    .Where(f => f.Flight.LandingTime >= slot.Slot.StartTime &&
                               f.Flight.LandingTime < slot.Slot.EndTime)
                    .ToList();

                foreach (var flightItem in flightsDuringSlot)
                {
                    Serilog.Log.Warning("Flight {Callsign} scheduled during slot on {Runway}: landing at {LandingTime}, slot {SlotStart} to {SlotEnd}",
                        flightItem.Flight.Callsign, runwayId, flightItem.Flight.LandingTime,
                        slot.Slot.StartTime, slot.Slot.EndTime);
                }
            }

            // Check for flights at runway mode boundaries
            foreach (var change in runwayModeChanges)
            {
                var flightsAtBoundary = flightsOnRunway
                    .Where(f => f.Flight.LandingTime == change.LastLandingTimeInPreviousMode)
                    .ToList();

                if (flightsAtBoundary.Count > 1)
                {
                    var callsigns = string.Join(", ", flightsAtBoundary.Select(f => f.Flight.Callsign));
                    Serilog.Log.Error("CRITICAL: Multiple flights at runway mode boundary on {Runway} at {BoundaryTime}: {Callsigns}",
                        runwayId, change.LastLandingTimeInPreviousMode, callsigns);
                }
            }
        }
    }

    void ValidateNoOverlapsOnRunway(string runwayIdentifier, Flight justScheduledFlight)
    {
        var flightsOnRunway = _sequence.OfType<FlightSequenceItem>()
            .Where(f => f.Flight.AssignedRunwayIdentifier == runwayIdentifier)
            .OrderBy(f => f.Flight.LandingTime)
            .ToList();

        // Check for exact time overlaps
        for (int i = 1; i < flightsOnRunway.Count; i++)
        {
            var previousFlight = flightsOnRunway[i - 1].Flight;
            var currentFlight = flightsOnRunway[i].Flight;

            if (previousFlight.LandingTime == currentFlight.LandingTime)
            {
                Serilog.Log.Error("CRITICAL OVERLAP DETECTED: Flights {Flight1} and {Flight2} both scheduled at {Time} on runway {Runway}",
                    previousFlight.Callsign, currentFlight.Callsign, currentFlight.LandingTime, runwayIdentifier);
            }
        }
    }

    interface ISequenceItem
    {
        DateTimeOffset Time { get; }
    }

    record FlightSequenceItem(Flight Flight) : ISequenceItem
    {
        // Always use LandingTime for sequence positioning to prevent discontinuities during state transitions
        public DateTimeOffset Time => Flight.LandingTime;
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
