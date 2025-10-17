using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
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

    readonly IArrivalConfigurationLookup _arrivalConfigurationLookup;
    readonly IArrivalLookup _arrivalLookup;
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

    public Sequence(Configuration.AirportConfiguration airportConfiguration, IArrivalLookup arrivalLookup, IClock clock, IArrivalConfigurationLookup arrivalConfigurationLookup)
    {
        _airportConfiguration = airportConfiguration;
        _arrivalLookup = arrivalLookup;
        _clock = clock;
        _arrivalConfigurationLookup = arrivalConfigurationLookup;

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

        var performanceData = AircraftPerformanceData.Default;
        var flight = new Flight(
                callsign,
                AirportIdentifier,
                DateTimeOffset.MinValue,
                DateTime.MinValue,
                performanceData.TypeCode,
                performanceData.AircraftCategory,
                performanceData.WakeCategory) // TODO: Need a new ctor
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

    public void Restore(SequenceMessage message)
    {
        // Clear existing state
        _sequence.Clear();
        _pendingFlights.Clear();
        _deSequencedFlights.Clear();

        // Restore dummy counter
        _dummyCounter = message.DummyCounter;

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
            InsertByTime(new SlotSequenceItem(slot), slot.StartTime, _sequence);
        }

        // Restore sequenced flights
        foreach (var flightMessage in message.Flights)
        {
            var flight = new Flight(flightMessage);
            InsertByTime(new FlightSequenceItem(flight), flight.LandingTime, _sequence);
        }

        // Restore pending flights
        foreach (var flightMessage in message.PendingFlights)
        {
            var flight = new Flight(flightMessage);
            _pendingFlights.Add(flight);
        }

        // Restore desequenced flights
        foreach (var flightMessage in message.DeSequencedFlights)
        {
            var flight = new Flight(flightMessage);
            _deSequencedFlights.Add(flight);
        }
    }

    public void Insert(Flight newFlight, DateTimeOffset preferredTime)
    {
        var index = InsertByTime(new FlightSequenceItem(newFlight), preferredTime, _sequence);

        // TODO: Which runway?
        Schedule(index, forceRescheduleStable: true, insertingFlights: [newFlight.Callsign]);
    }

    public void Insert(Flight newFlight, RelativePosition relativePosition, string referenceCallsign)
    {
        // BUG: No times for dummy flights, need to force a landing time and schedule everyone behind
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

        var newIndex = InsertByEstimate(flight, _sequence);

        var reschedulePoint = Math.Min(oldIndex, newIndex);

        Schedule(reschedulePoint, [flight.AssignedRunwayIdentifier]);
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
            if (currentFlight.State is State.Stable or State.SuperStable && !forceRescheduleStable)
            {
                continue;
            }

            // Skip flights on unaffected runways if runway filter is specified
            if (affectedRunways is not null &&
                !string.IsNullOrEmpty(currentFlight.AssignedRunwayIdentifier) &&
                !IsRunwayAffected(currentFlight.AssignedRunwayIdentifier, affectedRunways))
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

            // Assign a runway if not already assigned
            // TODO: It'd be nice to hide this away somewhere else
            Runway runway;
            if (string.IsNullOrEmpty(currentFlight.AssignedRunwayIdentifier))
            {
                if (string.IsNullOrEmpty(currentFlight.FeederFixIdentifier))
                {
                    runway = currentRunwayMode.Default;
                }
                else
                {
                    var matchingArrival = _arrivalConfigurationLookup
                        .GetArrivals()
                        .Where(a => a.AirportIdentifier == _airportConfiguration.Identifier)
                        .Where(a => a.FeederFixIdentifier == currentFlight.FeederFixIdentifier)
                        .Where(a => a.Category == currentFlight.AircraftCategory || a.AircraftTypes.Contains(currentFlight.AircraftType))
                        .Where(a => currentRunwayMode.Runways.Any(r => r.Identifier == a.RunwayIdentifier && r.ApproachType == a.ApproachType))
                        .FirstOrDefault();

                    if (matchingArrival is null)
                    {
                        // TODO: Log a warning
                    }

                    runway = matchingArrival is not null
                        ? currentRunwayMode.Runways.First(r => r.Identifier == matchingArrival.RunwayIdentifier && r.ApproachType == matchingArrival.ApproachType)
                        : currentRunwayMode.Default;
                }

                currentFlight.SetRunway(runway.Identifier, manual: false);
                currentFlight.ChangeApproachType(runway.ApproachType);
            }
            else
            {
                runway = currentRunwayMode.Runways
                    .FirstOrDefault(r => r.Identifier == currentFlight.AssignedRunwayIdentifier);
                if (runway is null)
                {
                    // Assigned to off-mode runway, fudge the details
                    runway = new Runway(
                        currentFlight.AssignedRunwayIdentifier,
                        currentFlight.ApproachType,
                        currentRunwayMode.OffModeSeparation,
                        _airportConfiguration.Runways.Select(s => new RunwayDependency(s, currentRunwayMode.OffModeSeparation)).ToArray());
                }
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

            // Ensure the flight isn't sped up to meet the previous item
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

                // If the landing time is in conflict with the next item, we may need to move this flight behind it
                if (landingTime.IsAfter(earliestTimeToTrailer))
                {
                    var isNewFlight = insertingFlights?.Contains(currentFlight.Callsign) ?? false;
                    var canDelayNextItem = nextItem switch
                    {
                        // Slots and runway mode changes can't be delayed
                        SlotSequenceItem => false,
                        RunwayModeChangeSequenceItem => false,

                        // New flights can delay stable flights
                        FlightSequenceItem { Flight.State: State.Stable } when isNewFlight => true,

                        // forceRescheduleStable allows stable and superstable flights to be delayed
                        FlightSequenceItem { Flight.State: State.Stable or State.SuperStable } when isNewFlight || forceRescheduleStable => true,

                        // Unstable flights can always be delayed
                        FlightSequenceItem { Flight.State: State.Unstable } => true,

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

                        // Decrement i to reprocess this index since we've moved something new into this position
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
        // Check for duplicate flights - prevent the same flight from being inserted multiple times
        if (item is FlightSequenceItem flightItem)
        {
            var existingFlight = sequence.OfType<FlightSequenceItem>()
                .FirstOrDefault(f => f.Flight.Callsign == flightItem.Flight.Callsign);

            if (existingFlight is not null)
            {
                var existingIndex = sequence.IndexOf(existingFlight);
                throw new MaestroException($"Flight {flightItem.Flight.Callsign} already exists in sequence at position {existingIndex}.");
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
        var finalIndex = sequence.Count - 1;
        return finalIndex;
    }

    int InsertByEstimate(Flight flight, List<ISequenceItem> sequence)
    {
        var existingFlight = sequence.OfType<FlightSequenceItem>()
            .FirstOrDefault(f => f.Flight.Callsign == flight.Callsign);

        if (existingFlight is not null)
        {
            var existingIndex = sequence.IndexOf(existingFlight);
            throw new MaestroException($"Flight {flight.Callsign} already exists in sequence at position {existingIndex}.");
        }

        for (var i = 0; i < sequence.Count; i++)
        {
            var time = sequence[i] switch
            {
                FlightSequenceItem flightItem => flightItem.Flight.LandingEstimate,
                _ => sequence[i].Time
            };

            if (flight.LandingEstimate > time)
                continue;

            sequence.Insert(i, new FlightSequenceItem(flight));
            return i;
        }

        sequence.Add(new FlightSequenceItem(flight));
        var finalIndex = sequence.Count - 1;
        return finalIndex;
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

    void Schedule(Flight flight, DateTimeOffset landingTime, string runwayIdentifier)
    {
        flight.SetLandingTime(landingTime);
        flight.SetRunway(runwayIdentifier, manual: flight.RunwayManuallyAssigned);

        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && flight.FeederFixEstimate is not null && !flight.HasPassedFeederFix)
        {
            var timeToGo = _arrivalLookup.GetTimeToGo(flight);
            var feederFixTime = flight.LandingTime.Subtract(timeToGo);
            flight.SetFeederFixTime(feederFixTime);
        }

        flight.ResetInitialEstimates();
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
