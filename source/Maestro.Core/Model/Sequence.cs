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

    private int _dummyCounter = 1;

    readonly List<Flight> _pendingFlights = [];
    readonly List<Flight> _deSequencedFlights = [];
    readonly List<ISequenceItem> _sequence = [];

    public string AirportIdentifier { get; }
    public IReadOnlyList<Flight> PendingFlights => _pendingFlights.AsReadOnly();
    public IReadOnlyList<Flight> DeSequencedFlights => _deSequencedFlights.AsReadOnly();
    // public IReadOnlyList<Flight> Flights => _trackedFlights.AsReadOnly();

    // public RunwayMode CurrentRunwayMode { get; private set; }
    // public DateTimeOffset LastLandingTimeForCurrentMode { get; private set; }
    // public RunwayMode? NextRunwayMode { get; private set; }
    // public DateTimeOffset FirstLandingTimeForNextMode { get; private set; }

    // public IReadOnlyList<Slot> Slots => _slots.AsReadOnly();

    public Sequence(Configuration.AirportConfiguration airportConfiguration, IArrivalLookup arrivalLookup, IPerformanceLookup performanceLookup)
    {
        _arrivalLookup = arrivalLookup;
        _performanceLookup = performanceLookup;

        AirportIdentifier = airportConfiguration.Identifier;
        // CurrentRunwayMode = new RunwayMode(airportConfiguration.RunwayModes.First());
        _sequence.Add(
            new RunwayModeStartSequenceItem(
                new RunwayMode(airportConfiguration.RunwayModes.First()),
                DateTimeOffset.MinValue));
    }

    // public Sequence(Configuration.AirportConfiguration airportConfiguration, SequenceMessage message)
    // {
    //     AirportIdentifier = message.AirportIdentifier;
    //
    //     var currentRunwayModeConfig = airportConfiguration.RunwayModes
    //         .Single(rm => rm.Identifier == message.CurrentRunwayMode.Identifier);
    //     CurrentRunwayMode = new RunwayMode(currentRunwayModeConfig);
    //
    //     var nextRunwayModeConfig = airportConfiguration.RunwayModes
    //         .SingleOrDefault(rm => rm.Identifier == message.NextRunwayMode?.Identifier);
    //     if (nextRunwayModeConfig is not null)
    //     {
    //         NextRunwayMode = new RunwayMode(nextRunwayModeConfig);
    //         LastLandingTimeForCurrentMode = message.LastLandingTimeForCurrentMode;
    //         FirstLandingTimeForNextMode = message.FirstLandingTimeForNextMode;
    //     }
    //
    //     // TODO:
    //     // _trackedFlights.AddRange(message.Flights.Select(f => new Flight(f)));
    //     // _slots.AddRange(message.Slots.Select(s => new Slot(s)));
    // }

    /// <summary>
    ///     Changes the runway mode with an immediate effect.
    /// </summary>
    public void ChangeRunwayMode(RunwayMode runwayMode, IClock clock)
    {
        var now = clock.UtcNow();

        // Start the new runway mode immediately
        InsertByTime(new RunwayModeStartSequenceItem(runwayMode, now), now, _sequence);

        // TODO: Trigger recompute for all affected flights
    }

    /// <summary>
    ///     Schedules a runway mode change for some time in the future.
    /// </summary>
    public void ChangeRunwayMode(
        RunwayMode runwayMode,
        DateTimeOffset lastLandingTimeForOldMode,
        DateTimeOffset firstLandingTimeForNewMode)
    {
        InsertByTime(new RunwayModeEndSequenceItem(lastLandingTimeForOldMode), lastLandingTimeForOldMode, _sequence);
        InsertByTime(new RunwayModeStartSequenceItem(runwayMode, firstLandingTimeForNewMode), firstLandingTimeForNewMode, _sequence);

        // TODO: Trigger recompute for all affected flights
    }

    public void AddDummyFlight(DateTimeOffset landingTime, string runwayIdentifier, IClock clock)
    {
        var callsign = $"****{_dummyCounter++:00}*";

        var flight = new Flight(callsign, AirportIdentifier, landingTime)
        {
            IsDummy = true
        };

        flight.SetRunway(runwayIdentifier, manual: true);
        flight.SetLandingTime(landingTime, manual: true);

        InsertByTime(new FlightSequenceItem(flight), landingTime, _sequence);
        flight.SetState(State.Frozen, clock);
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

        _sequence.RemoveAll(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);
        _deSequencedFlights.Add(flight);
    }

    public void Resume(string callsign)
    {
        var flight = _deSequencedFlights.FirstOrDefault(f => f.Callsign == callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found");

        Recompute(flight);
    }

    public void Remove(string callsign, IScheduler scheduler)
    {
        var flight = FindTrackedFlight(callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found in desequenced list");

        flight.Remove();
        scheduler.Schedule(this);
    }

    public void CreateSlot(DateTimeOffset start, DateTimeOffset end, string[] runwayIdentifiers)
    {
        var id = Guid.NewGuid();
        InsertByTime(new SlotStartSequenceItem(id, start, runwayIdentifiers), start, _sequence);
        InsertByTime(new SlotEndSequenceItem(id, end), end, _sequence);

        // TODO: Trigger recompute for all affected flights
    }

    public void ModifySlot(Guid id, DateTimeOffset start, DateTimeOffset end)
    {
        var startItem = _sequence.OfType<SlotStartSequenceItem>().FirstOrDefault(s => s.Id == id);
        var endItem = _sequence.OfType<SlotEndSequenceItem>().FirstOrDefault(s => s.Id == id);
        if (startItem is null || endItem is null)
            throw new MaestroException("Slot not found");

        _sequence.Remove(startItem);
        _sequence.Remove(endItem);

        CreateSlot(start, end, startItem.Runways);
    }

    public void DeleteSlot(Guid id)
    {
        var startItem = _sequence.OfType<SlotStartSequenceItem>().FirstOrDefault(s => s.Id == id);
        var endItem = _sequence.OfType<SlotEndSequenceItem>().FirstOrDefault(s => s.Id == id);
        if (startItem is null || endItem is null)
            throw new MaestroException("Slot not found");

        _sequence.Remove(startItem);
        _sequence.Remove(endItem);

        // TODO: Trigger recompute for all affected flights
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
            .OfType<SlotStartSequenceItem>()
            .Select(start => new Slot(
                start.Id,
                start.Time,
                _sequence.OfType<SlotEndSequenceItem>().FirstOrDefault(end => end.Id == start.Id)?.Time ?? start.Time,
                start.Runways).ToMessage())
            .ToArray();

        var currentRunwayModeStart = _sequence.OfType<RunwayModeStartSequenceItem>().First();
        var currentRunwayMode = currentRunwayModeStart.RunwayMode;

        var nextRunwayModeStart = _sequence
            .OfType<RunwayModeStartSequenceItem>()
            .FirstOrDefault(i => i != currentRunwayModeStart);
        var nextRunwayMode = nextRunwayModeStart?.RunwayMode;
        var lastLandingTimeForCurrentMode = _sequence
            .OfType<RunwayModeEndSequenceItem>()
            .FirstOrDefault()?.EndTime ?? default;
        var firstLandingTimeForNextMode = nextRunwayModeStart?.StartTime ?? default;

        return new SequenceMessage
        {
            AirportIdentifier = AirportIdentifier,
            Flights = sequencedFlights,
            PendingFlights = _pendingFlights.Select(f => f.ToMessage(this)).ToArray(),
            DeSequencedFlights = _deSequencedFlights.Select(f => f.ToMessage(this)).ToArray(),
            CurrentRunwayMode = currentRunwayMode.ToMessage(),
            NextRunwayMode = nextRunwayMode?.ToMessage(),
            LastLandingTimeForCurrentMode = lastLandingTimeForCurrentMode,
            FirstLandingTimeForNextMode = firstLandingTimeForNextMode,
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

    public void Insert(Flight newFlight)
    {
        InsertNewFlightByTime(new FlightSequenceItem(newFlight), newFlight.LandingEstimate, _sequence);
        Schedule(_sequence);
    }

    public void Recompute(Flight flight)
    {
        // Remove and re-insert the flight by it's landing estimate
        _sequence.RemoveAll(i => i is FlightSequenceItem f && f.Flight == flight);
        InsertByTime(new FlightSequenceItem(flight), flight.LandingEstimate, _sequence);

        Schedule(_sequence);
    }

    public void MakePending(Flight flight)
    {
        flight.Reset();
        _pendingFlights.Add(flight);
        _sequence.RemoveAll(i => i is FlightSequenceItem flightSequenceItem && flightSequenceItem.Flight == flight);
    }

    // List<ISequenceItem> BuildSequence()
    // {
    //     var orderedSequence = new List<ISequenceItem>();
    //
    //     // Landed flights are whatever
    //     foreach (var flight in sequence.Flights.Where(f => f.State == State.Landed).OrderBy(f => f.LandingTime))
    //     {
    //         orderedSequence.Add(new FlightSequenceItem(flight));
    //     }
    //
    //     // Start with the current runway mode
    //     orderedSequence.Add(new RunwayModeStartSequenceItem(sequence.CurrentRunwayMode, DateTimeOffset.MinValue));
    //     if (sequence.NextRunwayMode is not null)
    //     {
    //         orderedSequence.Add(new RunwayModeEndSequenceItem(sequence.CurrentRunwayMode, sequence.LastLandingTimeForCurrentMode));
    //         orderedSequence.Add(new RunwayModeStartSequenceItem(sequence.NextRunwayMode, sequence.FirstLandingTimeForNextMode));
    //     }
    //
    //     // Insert slots to sequence around
    //     foreach (var slot in sequence.Slots.OrderBy(s => s.StartTime))
    //     {
    //         orderedSequence.Add(new SlotStartSequenceItem(slot));
    //         orderedSequence.Add(new SlotEndSequenceItem(slot));
    //     }
    //
    //     // Frozen flights can aren't affected by slots
    //     foreach (var flight in sequence.Flights.Where(f => f.State == State.Frozen).OrderBy(f => f.LandingTime))
    //     {
    //         orderedSequence.Add(new FlightSequenceItem(flight));
    //     }
    //
    //     // Ensure everything is in the correct order before we start inserting flights
    //     // This accounts for shorter slots that start after and end before longer slots
    //     orderedSequence = orderedSequence.OrderBy(s => s.Time).ToList();
    //
    //     // SuperStable flights
    //     foreach (var flight in sequence.Flights.Where(f => f.State is State.SuperStable).OrderBy(f => f.LandingTime))
    //     {
    //         InsertByTime(new FlightSequenceItem(flight), flight.LandingTime, orderedSequence);
    //     }
    //
    //     // Stable flights are next
    //     foreach (var flight in sequence.Flights.Where(f => f.State is State.SuperStable or State.Stable).OrderBy(f => f.LandingTime))
    //     {
    //         InsertByTime(new FlightSequenceItem(flight), flight.LandingTime, orderedSequence);
    //     }
    //
    //     // Unstable flights are inserted by their estimate
    //     foreach (var flight in sequence.Flights.Where(f => f.State is State.Unstable).OrderBy(f => f.LandingEstimate))
    //     {
    //         InsertByTime(new FlightSequenceItem(flight), flight.LandingEstimate, orderedSequence);
    //     }
    //
    //     return orderedSequence;
    // }

    void Schedule(List<ISequenceItem> sequence)
    {
        for (var i = 0; i < sequence.Count; i++)
        {
            if (i == 0)
                continue;

            var currentItem = sequence[i];
            if (currentItem is not FlightSequenceItem flightItem)
                continue;

            var currentFlight = flightItem.Flight;
            if (currentFlight.State is State.Landed or State.Frozen or State.SuperStable or State.Stable) // TODO: ???
                continue;

            var precedingItems = sequence.Take(i);
            var runwayModeItem = precedingItems.LastOrDefault(s => s is RunwayModeStartSequenceItem) as RunwayModeStartSequenceItem;
            if (runwayModeItem is null)
                throw new Exception("No runway mode found");

            var preferredLandingTime = currentItem.Time;
            var (bestLandingTime, bestRunway) = FindOptimalRunwayAndTime(
                runwayModeItem.RunwayMode,
                preferredLandingTime,
                precedingItems);

            // Check if the best landing time is after the current runway mode ends
            var runwayModeEnd = precedingItems
                .OfType<RunwayModeEndSequenceItem>()
                .Where(end => end.RunwayMode == runwayModeItem.RunwayMode)
                .FirstOrDefault();

            if (runwayModeEnd is not null && bestLandingTime >= runwayModeEnd.Time)
            {
                // Landing time is after current mode ends, need to use next runway mode
                var nextRunwayMode = precedingItems
                    .OfType<RunwayModeStartSequenceItem>()
                    .Where(start => start.Time > runwayModeEnd.Time)
                    .FirstOrDefault();

                if (nextRunwayMode is not null)
                {
                    // Recalculate with next runway mode
                    (bestLandingTime, bestRunway) = FindOptimalRunwayAndTime(
                        nextRunwayMode.RunwayMode,
                        nextRunwayMode.Time, // Start from next mode start time
                        precedingItems);
                }
            }

            var landingTime = bestLandingTime;
            var assignedRunway = bestRunway;

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

            Schedule(currentFlight, landingTime, assignedRunway.Identifier, performance);
        }
    }

    void InsertByTime(ISequenceItem item, DateTimeOffset dateTimeOffset, List<ISequenceItem> sequence)
    {
        if (item is not FlightSequenceItem flightItem)
        {
            // Non-flight items use simple time-based insertion
            for (int i = 0; i < sequence.Count; i++)
            {
                if (dateTimeOffset >= sequence[i].Time)
                    continue;

                sequence.Insert(i, item);
                return;
            }

            sequence.Add(item);
            return;
        }

        // For flights, avoid inserting between slot start/end for same runway
        for (int i = 0; i < sequence.Count; i++)
        {
            if (dateTimeOffset >= sequence[i].Time)
                continue;

            // Check if we're trying to insert between a slot start and end for this runway
            if (IsInsertionBlockedBySlot(i, flightItem.Flight.AssignedRunwayIdentifier, sequence))
                continue;

            sequence.Insert(i, item);
            return;
        }

        sequence.Add(item);
    }

    void InsertNewFlightByTime(FlightSequenceItem flightItem, DateTimeOffset preferredTime, List<ISequenceItem> sequence)
    {
        for (int i = 0; i < sequence.Count; i++)
        {
            if (preferredTime >= sequence[i].Time)
                continue;

            // New flights cannot be inserted before SuperStable, Frozen, or Landed flights
            if (sequence[i] is FlightSequenceItem existingFlight &&
                existingFlight.Flight.State is State.SuperStable or State.Frozen or State.Landed)
                continue;

            // Check if we're trying to insert between a slot start and end for this runway
            if (IsInsertionBlockedBySlot(i, flightItem.Flight.AssignedRunwayIdentifier, sequence))
                continue;

            sequence.Insert(i, flightItem);
            return;
        }

        sequence.Add(flightItem);
    }

    bool IsInsertionBlockedBySlot(int insertionIndex, string? runwayIdentifier, List<ISequenceItem> sequence)
    {
        if (string.IsNullOrEmpty(runwayIdentifier))
            return false;

        // Look backwards from insertion point to find if we're in an active slot or runway mode transition
        for (int j = insertionIndex - 1; j >= 0; j--)
        {
            var precedingItem = sequence[j];

            // Check for slot boundaries
            if (precedingItem is SlotEndSequenceItem slotEnd)
            {
                var slotRunways = sequence
                    .OfType<SlotStartSequenceItem>()
                    .Where(s => s.Id == slotEnd.Id)
                    .SelectMany(s => s.Runways)
                    .ToArray();
                if (slotRunways.Contains(runwayIdentifier))
                {
                    // Found slot end for this runway, we're not in a slot
                    return false;
                }
            }

            if (precedingItem is SlotStartSequenceItem slotStart && slotStart.Runways.Contains(runwayIdentifier))
            {
                // Found slot start without matching end, we're inside a slot
                return true;
            }

            // Check for runway mode boundaries
            if (precedingItem is RunwayModeStartSequenceItem runwayModeStart &&
                runwayModeStart.RunwayMode.Runways.Any(r => r.Identifier == runwayIdentifier))
            {
                // Found runway mode start, we're in an active runway mode
                return false;
            }

            if (precedingItem is RunwayModeEndSequenceItem)
            {
                // Found runway mode end without matching start, we're in a transition gap
                return true;
            }
        }

        return false;
    }

    (DateTimeOffset LandingTime, Runway Runway) FindOptimalRunwayAndTime(
        RunwayMode runwayMode,
        DateTimeOffset preferredTime,
        IEnumerable<ISequenceItem> precedingItems)
    {
        var bestLandingTime = preferredTime;
        var bestRunway = runwayMode.Default;

        foreach (var runway in runwayMode.Runways)
        {
            var earliestLandingTime = CalculateEarliestLandingTimeForRunway(
                runway,
                preferredTime,
                precedingItems);

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
        IEnumerable<ISequenceItem> precedingItems)
    {
        var earliestTime = preferredTime;

        // Find the most recent preceding flight on this runway
        var lastFlightOnRunway = precedingItems
            .OfType<FlightSequenceItem>()
            .Where(f => f.Flight.AssignedRunwayIdentifier == runway.Identifier)
            .LastOrDefault();

        if (lastFlightOnRunway is not null)
        {
            var minTimeAfterPrecedingFlight = lastFlightOnRunway.Flight.LandingTime.Add(runway.AcceptanceRate);
            if (minTimeAfterPrecedingFlight > earliestTime)
                earliestTime = minTimeAfterPrecedingFlight;
        }

        // Check for slot conflicts on this runway
        var activeSlot = precedingItems
            .OfType<SlotStartSequenceItem>()
            .Where(slot => slot.Runways.Contains(runway.Identifier))
            .Where(slot => slot.Time <= earliestTime)
            .LastOrDefault();

        if (activeSlot is not null)
        {
            // Check if we have a corresponding slot end after the active slot start
            var slotEnd = precedingItems
                .OfType<SlotEndSequenceItem>()
                .Where(end => end.Runways.Contains(runway.Identifier))
                .Where(end => end.Time > activeSlot.Time)
                .FirstOrDefault();

            if (slotEnd is null || earliestTime < slotEnd.Time)
            {
                // We're in an active slot, delay until after slot ends
                // For now, find the slot end time (this could be improved)
                var nextSlotEnd = precedingItems
                    .OfType<SlotEndSequenceItem>()
                    .Where(end => end.Runways.Contains(runway.Identifier))
                    .Where(end => end.Time >= earliestTime)
                    .FirstOrDefault();

                if (nextSlotEnd is not null)
                    earliestTime = nextSlotEnd.Time;
            }
        }

        return earliestTime;
    }

    void Schedule(Flight flight, DateTimeOffset landingTime, string runwayIdentifier, AircraftPerformanceData performanceData)
    {
        flight.SetLandingTime(landingTime);
        flight.SetRunway(runwayIdentifier, manual: flight.RunwayManuallyAssigned);

        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && flight.FeederFixEstimate is not null && !flight.HasPassedFeederFix)
        {
            var arrivalInterval = arrivalLookup.GetArrivalInterval(
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
                _logger.Warning("Could not update feeder fix time for {Callsign}, no arrival interval found", flight.Callsign);
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
        public DateTimeOffset Time => Flight.LandingTime;
    }

    record SlotStartSequenceItem(Guid Id, DateTimeOffset Time, string[] Runways) : ISequenceItem;
    record SlotEndSequenceItem(Guid Id, DateTimeOffset Time) : ISequenceItem;

    record RunwayModeEndSequenceItem(DateTimeOffset EndTime) : ISequenceItem
    {
        public DateTimeOffset Time => EndTime;
    }

    record RunwayModeStartSequenceItem(RunwayMode RunwayMode, DateTimeOffset StartTime) : ISequenceItem
    {
        public DateTimeOffset Time => StartTime;
    }
}
