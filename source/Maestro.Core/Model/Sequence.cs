using System.Diagnostics.SymbolStore;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

// TODO: Need to consolidate all of these methods here. Maybe move them into individual handlers?

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

    public Sequence(Configuration.AirportConfiguration airportConfiguration, IArrivalLookup arrivalLookup, IClock clock)
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
        var index = CalculateInsertionIndex(now);
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
        var index = CalculateInsertionIndex(lastLandingTimeForOldMode);
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

    public void InsertDummyFlight(
        string callsign,
        string aircraftTypeCode,
        DateTimeOffset landingTime,
        string[] runwayIdentifiers,
        State state)
    {
        var runwayMode = GetRunwayModeAt(landingTime);
        var runwayIdentifier = runwayIdentifiers.FirstOrDefault(r => runwayMode.Runways.Any(rm => rm.Identifier == r))
                     ?? runwayMode.Default.Identifier;

        var flight = new Flight(
            callsign,
            aircraftTypeCode,
            AirportIdentifier,
            runwayIdentifier,
            landingTime,
            state);

        var index = CalculateInsertionIndex(landingTime);
        ValidateInsertionBetweenImmovableFlights(index, runwayIdentifier);

        InsertAt(
            index,
            new FlightSequenceItem(flight));

        Schedule(index, [runwayIdentifier], forceRescheduleStable: true, insertingFlights: [flight.Callsign]);
    }

    public void InsertDummyFlight(
        string callsign,
        string aircraftTypeCode,
        RelativePosition relativePosition,
        string referenceCallsign,
        State state)
    {
        var referenceFlightItem = _sequence
            .OfType<FlightSequenceItem>()
            .FirstOrDefault(i => i.Flight.Callsign == referenceCallsign);
        if (referenceFlightItem is null)
            throw new MaestroException($"{referenceCallsign} not found");

        var referenceFlightIndex = _sequence.IndexOf(referenceFlightItem);
        var flightInsertionIndex = relativePosition switch
        {
            RelativePosition.Before => referenceFlightIndex,
            RelativePosition.After => referenceFlightIndex + 1,
            _ => throw new ArgumentOutOfRangeException()
        };

        ValidateInsertionBetweenImmovableFlights(flightInsertionIndex, referenceFlightItem.Flight.AssignedRunwayIdentifier);

        // TODO: Source the runway mode from the flight instead of calculating it
        var runwayMode = GetRunwayModeAt(referenceFlightItem.Time);
        var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlightItem.Flight.AssignedRunwayIdentifier);

        var landingTime = relativePosition switch
        {
            RelativePosition.Before => referenceFlightItem.Flight.LandingTime,
            RelativePosition.After when runway is not null => referenceFlightItem.Flight.LandingTime.Add(
                runway.AcceptanceRate),
            _ => throw new ArgumentOutOfRangeException(nameof(relativePosition), relativePosition, null)
        };

        var flight = new Flight(
            callsign,
            aircraftTypeCode,
            AirportIdentifier,
            referenceFlightItem.Flight.AssignedRunwayIdentifier,
            landingTime,
            state);

        // Now perform the actual insertion
        InsertAt(flightInsertionIndex, new FlightSequenceItem(flight));

        Schedule(
            flightInsertionIndex,
            [flight.AssignedRunwayIdentifier],
            forceRescheduleStable: true);
    }

    public string NewDummyCallsign()
    {
        return $"****{_dummyCounter++:00}*";
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

        var index = CalculateInsertionIndex(flight.LandingEstimate);
        InsertAt(index, new FlightSequenceItem(flight));

        // TODO: Test that resumed flights are pushed back until there is a spot available
        Schedule(index, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    public void Remove(string callsign)
    {
        // Sequenced flights (both real and manually-inserted)
        var flightItem = _sequence.OfType<FlightSequenceItem>()
            .FirstOrDefault(i => i.Flight.Callsign == callsign);
        if (flightItem is not null)
        {
            // TODO: Reset the flight and place it into the pending list
            Remove(flightItem);
            flightItem.Flight.Reset();
            _pendingFlights.Add(flightItem.Flight);
            return;
        }

        // Desequenced flights
        var desequencedFlight = _deSequencedFlights.FirstOrDefault(f => f.Callsign == callsign);
        if (desequencedFlight is not null)
        {
            _deSequencedFlights.Remove(desequencedFlight);
            desequencedFlight.Reset();
            _pendingFlights.Add(desequencedFlight);
            return;
        }

        throw new MaestroException($"Could not remove {callsign} as it was not found in the sequence");
    }

    void Remove(ISequenceItem sequenceItem)
    {
        var index = _sequence.IndexOf(sequenceItem);
        if (index == -1)
            throw new MaestroException($"Item {sequenceItem} not found");

        _sequence.RemoveAt(index);

        var runwayIdentifiers = sequenceItem switch
        {
            FlightSequenceItem flightItem => [flightItem.Flight.AssignedRunwayIdentifier],
            SlotSequenceItem slotItem => slotItem.Slot.RunwayIdentifiers.ToHashSet(),
            RunwayModeChangeSequenceItem runwayModeChangeItem => runwayModeChangeItem.RunwayMode.Runways.Select(r => r.Identifier).ToHashSet(),
            _ => []
        };

        Schedule(index, runwayIdentifiers, forceRescheduleStable: true);
    }

    public Guid CreateSlot(DateTimeOffset start, DateTimeOffset end, string[] runwayIdentifiers)
    {
        var id = Guid.NewGuid();
        var index = CalculateInsertionIndex(start);
        InsertAt(index, new SlotSequenceItem(new Slot(id, start, end, runwayIdentifiers)));

        Schedule(index, runwayIdentifiers.ToHashSet(), forceRescheduleStable: true);
        return id;
    }

    public void ModifySlot(Guid id, DateTimeOffset start, DateTimeOffset end)
    {
        var slotItem = _sequence.OfType<SlotSequenceItem>().FirstOrDefault(s => s.Slot.Id == id);
        if (slotItem is null)
            throw new MaestroException($"Slot {id} not found");

        slotItem.Slot.ChangeTime(start, end);

        // Reinsert at the correct position according to the start time
        _sequence.Remove(slotItem);
        var index = CalculateInsertionIndex(start);
        InsertAt(index, slotItem);

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
            var index = CalculateInsertionIndex(slot.StartTime);
            InsertAt(index, new SlotSequenceItem(slot));
        }

        // Restore sequenced flights (both real and manually-inserted)
        foreach (var flightMessage in message.Flights)
        {
            var flight = new Flight(flightMessage);
            var index = CalculateInsertionIndex(flight.LandingTime);
            InsertAt(index, new FlightSequenceItem(flight));
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
        var index = CalculateInsertionIndex(preferredTime);
        if (!string.IsNullOrEmpty(newFlight.AssignedRunwayIdentifier))
            ValidateInsertionBetweenImmovableFlights(index, newFlight.AssignedRunwayIdentifier!);

        // If the flight is pending, remove it from there
        _pendingFlights.Remove(newFlight);

        InsertAt(index, new FlightSequenceItem(newFlight));

        // TODO: Which runway?
        Schedule(index, forceRescheduleStable: true, insertingFlights: [newFlight.Callsign]);
    }

    public void Insert(Flight newFlight, RelativePosition relativePosition, string referenceCallsign)
    {
        // BUG: No times for dummy flights, need to force a landing time and schedule everyone behind
        var index = CalculateInsertionIndex(relativePosition, referenceCallsign);

        // Experiment: Don't put the flight directly before/after the reference flight if it's estimate is way out
        // Try to move it closer to its estimate while still respecting the relative position
        var indexByEstimate = CalculateInsertionIndex(newFlight.LandingEstimate);
        index = relativePosition switch
        {
            RelativePosition.Before when indexByEstimate < index => indexByEstimate,
            RelativePosition.After when indexByEstimate > index => indexByEstimate,
            _ => index
        };

        if (!string.IsNullOrEmpty(newFlight.AssignedRunwayIdentifier))
            ValidateInsertionBetweenImmovableFlights(index, newFlight.AssignedRunwayIdentifier!);

        InsertAt(index, new FlightSequenceItem(newFlight));

        Schedule(index, forceRescheduleStable: true, insertingFlights: [newFlight.Callsign]);
    }

    public void InsertPending(string callsign, DateTimeOffset landingTime, string[] runwayIdentifiers)
    {
        var pendingFlight = _pendingFlights.FirstOrDefault(f => f.Callsign == callsign);
        if (pendingFlight is null)
            throw new MaestroException($"{callsign} not found in pending list");

        var index = CalculateInsertionIndex(landingTime);
        if (!string.IsNullOrEmpty(pendingFlight.AssignedRunwayIdentifier))
            ValidateInsertionBetweenImmovableFlights(index, pendingFlight.AssignedRunwayIdentifier!);

        var runwayMode = GetRunwayModeAt(landingTime);
        var runwayIdentifier = runwayIdentifiers.FirstOrDefault(r => runwayMode.Runways.Any(rm => rm.Identifier == r))
                               ?? runwayMode.Default.Identifier;

        _pendingFlights.Remove(pendingFlight);
        pendingFlight.SetRunway(runwayIdentifier, manual: true);

        InsertAt(index, new FlightSequenceItem(pendingFlight));
        Schedule(index, forceRescheduleStable: true, insertingFlights: [pendingFlight.Callsign], affectedRunways: [runwayIdentifier]);
    }

    public void InsertPending(string callsign, RelativePosition relativePosition, string referenceCallsign)
    {
        var pendingFlight = _pendingFlights.FirstOrDefault(f => f.Callsign == callsign);
        if (pendingFlight is null)
            throw new MaestroException($"{callsign} not found in pending list");

        var index = CalculateInsertionIndex(relativePosition, referenceCallsign);

        // Experiment: Don't put the flight directly before/after the reference flight if it's estimate is way out
        // Try to move it closer to its estimate while still respecting the relative position
        var indexByEstimate = CalculateInsertionIndex(pendingFlight.LandingEstimate);
        index = relativePosition switch
        {
            RelativePosition.Before when indexByEstimate < index => indexByEstimate,
            RelativePosition.After when indexByEstimate > index => indexByEstimate,
            _ => index
        };

        if (!string.IsNullOrEmpty(pendingFlight.AssignedRunwayIdentifier))
            ValidateInsertionBetweenImmovableFlights(index, pendingFlight.AssignedRunwayIdentifier!);

        _pendingFlights.Remove(pendingFlight);

        InsertAt(index, new FlightSequenceItem(pendingFlight));
        Schedule(index, forceRescheduleStable: true, insertingFlights: [pendingFlight.Callsign]);
    }

    int CalculateInsertionIndex(RelativePosition relativePosition, string referenceCallsign)
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

        return insertionIndex;
    }

    public void Recompute(Flight flight)
    {
        var originalIndex = _sequence.FindIndex(i => i is FlightSequenceItem f && f.Flight == flight);

        // Validate BEFORE removing - exclude the flight being moved
        var newIndex = CalculateInsertionIndex(flight.LandingEstimate);
        if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
            ValidateInsertionBetweenImmovableFlights(newIndex, flight.AssignedRunwayIdentifier!);

        // Remove and re-insert the flight by it's landing estimate
        _sequence.RemoveAll(i => i is FlightSequenceItem f && f.Flight == flight);

        InsertAt(newIndex, new FlightSequenceItem(flight));

        var recomputePoint = Math.Min(originalIndex, newIndex);

        Schedule(recomputePoint, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    public void MakePending(Flight flight)
    {
        flight.Reset();
        _pendingFlights.Add(flight);
        _sequence.RemoveAll(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);
    }

    public void Depart(Flight flight, IInsertFlightOptions options)
    {
        // TODO: Move some of this into the handler
        _pendingFlights.Remove(flight);

        int index;
        switch (options)
        {
            case ExactInsertionOptions landingTimeOption:
                flight.SetLandingTime(landingTimeOption.TargetLandingTime, manual: true);

                var runwayMode = GetRunwayModeAt(landingTimeOption.TargetLandingTime);
                var runwayIdentifier = runwayMode.Runways.FirstOrDefault(r => landingTimeOption.RunwayIdentifiers.Contains(r.Identifier))?.Identifier
                    ?? runwayMode.Default.Identifier;
                flight.SetRunway(runwayIdentifier, manual: true);

                index = CalculateInsertionIndex(landingTimeOption.TargetLandingTime);
                break;

            case RelativeInsertionOptions relativeInsertionOptions:
                // TODO: Copied from InsertRelative, but extracted so we can adjust the landing time.
                // Needs refactoring.

                // Check for duplicate flights - prevent the same flight from being inserted multiple times
                var existingFlight = _sequence.OfType<FlightSequenceItem>()
                    .FirstOrDefault(f => f.Flight.Callsign == flight.Callsign);

                if (existingFlight is not null)
                {
                    throw new MaestroException($"Flight {flight.Callsign} already exists in sequence at position {_sequence.IndexOf(existingFlight)}.");
                }

                var referenceFlightItem = _sequence
                    .OfType<FlightSequenceItem>()
                    .FirstOrDefault(i => i.Flight.Callsign == relativeInsertionOptions.ReferenceCallsign);
                if (referenceFlightItem is null)
                    throw new MaestroException(
                        $"Reference flight {relativeInsertionOptions.ReferenceCallsign} not found");

                var referenceIndex = _sequence.IndexOf(referenceFlightItem);
                if (referenceIndex == -1)
                    throw new MaestroException("Reference flight not found in sequence");

                var insertionIndex = relativeInsertionOptions.Position switch
                {
                    RelativePosition.Before => referenceIndex,
                    RelativePosition.After => referenceIndex + 1,
                    _ => throw new ArgumentOutOfRangeException()
                };

                ValidateInsertionBetweenImmovableFlights(insertionIndex, flight.AssignedRunwayIdentifier);

                // BUG: This will initially cause a conflict because the reference flight doesn't get recomputed here.
                // They will be recomputed afterwards as part of regular scheduling, but if they're stable, it's likely to cause a persistent conflict until manually adjusted.
                // Need to figure out a better way to deal with inserting flights at specific times.
                flight.SetLandingTime(referenceFlightItem.Flight.LandingTime, manual: true);

                index = insertionIndex;

                break;

            case DepartureInsertionOptions departureInsertionOptions:
                if (flight.EstimatedTimeEnroute is null)
                    throw new MaestroException("Flight has no EET");

                var landingEstimate = departureInsertionOptions.TakeoffTime.Add(flight.EstimatedTimeEnroute.Value);
                flight.UpdateLandingEstimate(landingEstimate);
                index = CalculateInsertionIndex(landingEstimate);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(options));
        }

        InsertAt(index, new FlightSequenceItem(flight));
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

        // Calculate insertion index and validate BEFORE removing/mutating - exclude the flight being moved
        var insertionIndex = CalculateInsertionIndex(newLandingTime);
        ValidateInsertionBetweenImmovableFlights(insertionIndex, runway.Identifier);

        // Remove flight from current position
        _sequence.RemoveAll(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);

        // Now it's safe to mutate - validation has passed
        flight.SetRunway(runway.Identifier, manual: true);
        flight.SetLandingTime(newLandingTime, manual: true);

        // TODO: When moved to a position more than 1 runway separation from the preceding flight will, it should move forward towards this position to minimise the delay.
        var index = CalculateInsertionIndex(newLandingTime);
        InsertAt(index, new FlightSequenceItem(flight));

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

        // Validate BEFORE removing - exclude the flight being moved
        var newIndex = CalculateInsertionIndex(time);
        if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
            ValidateInsertionBetweenImmovableFlights(newIndex, flight.AssignedRunwayIdentifier!);

        var oldIndex = _sequence.IndexOf(existingFlight);
        _sequence.RemoveAt(oldIndex);

        if (newIndex > oldIndex)
            newIndex--;

        InsertAt(newIndex, existingFlight);

        var reschedulePoint = Math.Min(oldIndex, newIndex);

        Schedule(reschedulePoint, [flight.AssignedRunwayIdentifier]);
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

        // TODO: Source the runway mode from the flight instead of calculating it
        var runwayMode = GetRunwayModeAt(referenceFlightItem.Time);
        var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlightItem.Flight.AssignedRunwayIdentifier);

        var landingTime = relativePosition switch
        {
            RelativePosition.Before => referenceFlightItem.Flight.LandingTime,
            RelativePosition.After when runway is not null => referenceFlightItem.Flight.LandingTime.Add(runway.AcceptanceRate),
            _ => throw new ArgumentOutOfRangeException(nameof(relativePosition), relativePosition, null)
        };

        existingFlightItem.Flight.SetRunway(referenceFlightItem.Flight.AssignedRunwayIdentifier!, manual: true);
        existingFlightItem.Flight.SetLandingTime(landingTime);

        _sequence.Remove(existingFlightItem);
        if (currentFlightIndex >= insertionIndex)
            insertionIndex--;

        InsertAt(insertionIndex, existingFlightItem);

        var recomputeIndex = Math.Min(currentFlightIndex, insertionIndex);

        Schedule(recomputeIndex, [flight.AssignedRunwayIdentifier], forceRescheduleStable: true);
    }

    void InsertAt(int index, ISequenceItem item)
    {
        if (index >= _sequence.Count)
            _sequence.Add(item);
        else
            _sequence.Insert(index, item);
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

            // TODO: What about manual landing times?

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

                        // Manually-inserted flights can't be delayed
                        // TODO: Verify this behaviour is correct
                        FlightSequenceItem { Flight.IsManuallyInserted: true } => false,

                        // TODO: What about manual landing times?

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

    int CalculateInsertionIndex(DateTimeOffset dateTimeOffset)
    {
        for (var i = 0; i < _sequence.Count; i++)
        {
            if (dateTimeOffset > _sequence[i].Time)
                continue;

            return i;
        }

        return _sequence.Count;
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

    interface ISequenceItem
    {
        DateTimeOffset Time { get; }
    }

    interface IFlightSequenceItem : ISequenceItem
    {
        State State { get; }
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
