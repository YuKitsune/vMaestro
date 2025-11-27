using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public class Sequence
{
    object _gate = new object();

    readonly AirportConfiguration _airportConfiguration;

    // TODO: Figure out how to get rid of these from here
    readonly IArrivalLookup _arrivalLookup;
    readonly IClock _clock;

    readonly List<Slot> _slots = [];
    readonly List<Flight> _flights = [];

    public string AirportIdentifier { get; }

    public IReadOnlyList<Slot> Slots => _slots.AsReadOnly();
    public IReadOnlyList<Flight> Flights => _flights.AsReadOnly();

    public RunwayMode CurrentRunwayMode { get; private set; }
    public RunwayMode? NextRunwayMode { get; private set; }
    public DateTimeOffset? LastLandingTimeForCurrentMode { get; private set; }
    public DateTimeOffset? FirstLandingTimeForNewMode { get; private set; }

    public Sequence(AirportConfiguration airportConfiguration, IArrivalLookup arrivalLookup, IClock clock)
    {
        _airportConfiguration = airportConfiguration;
        _arrivalLookup = arrivalLookup;
        _clock = clock;

        AirportIdentifier = airportConfiguration.Identifier;
        CurrentRunwayMode = new RunwayMode(airportConfiguration.RunwayModes.First());
    }

    /// <summary>
    ///     Immediately changes the runway mode and recomputes the entire sequence.
    /// </summary>
    public void ChangeRunwayMode(RunwayMode runwayMode)
    {
        lock (_gate)
        {
            CurrentRunwayMode = runwayMode;
            Schedule(0, forceRescheduleStable: true);
        }
    }

    /// <summary>
    ///     Schedules a runway mode change for some time in the future, and recomputes the sequence from the point
    ///     where the new configuration becomes effective.
    /// </summary>
    public void ChangeRunwayMode(
        RunwayMode runwayMode,
        DateTimeOffset lastLandingTimeForOldMode,
        DateTimeOffset firstLandingTimeForNewMode)
    {
        lock (_gate)
        {
            NextRunwayMode = runwayMode;
            LastLandingTimeForCurrentMode = lastLandingTimeForOldMode;
            FirstLandingTimeForNewMode = firstLandingTimeForNewMode;

            var recomputeIndex = IndexOf(lastLandingTimeForOldMode);
            Schedule(recomputeIndex, forceRescheduleStable: true);
        }
    }

    public void TrySwapRunwayModes()
    {
        lock (_gate)
        {
            if (NextRunwayMode is null || LastLandingTimeForCurrentMode is null || FirstLandingTimeForNewMode is null)
                return;

            if (_clock.UtcNow().IsBefore(FirstLandingTimeForNewMode.Value))
                return;

            CurrentRunwayMode = NextRunwayMode;
            LastLandingTimeForCurrentMode = null;
            FirstLandingTimeForNewMode = null;
        }
    }

    /// <summary>
    ///     Returns the active <see cref="RunwayMode"/> at the specified <paramref name="time"/>.
    /// </summary>
    public RunwayMode GetRunwayModeAt(DateTimeOffset time)
    {
        lock (_gate)
        {
            if (NextRunwayMode is null || LastLandingTimeForCurrentMode is null || FirstLandingTimeForNewMode is null)
                return CurrentRunwayMode;

            if (time.IsBefore(FirstLandingTimeForNewMode.Value))
                return CurrentRunwayMode;

            return NextRunwayMode;
        }
    }

    /// <summary>
    ///     Throws a <see cref="MaestroException"/> if a flight <b>cannot</b> be inserted or moved to the provided
    ///     <paramref name="index"/>.
    /// </summary>
    public void ThrowIfSlotIsUnavailable(int index, string runwayIdentifier)
    {
        lock (_gate)
        {
            ValidateInsertionBetweenImmovableFlights(index, runwayIdentifier);
        }
    }

    /// <summary>
    ///     Inserts a <see cref="Flight"/> at the specified <paramref name="index"/>, and recomputes the sequence from
    ///     the point where the flight was inserted.
    /// </summary>
    public void Insert(int index, Flight flight)
    {
        lock (_gate)
        {
            ValidateInsertionBetweenImmovableFlights(index, flight.AssignedRunwayIdentifier);
            _flights.Insert(index, flight);
            Schedule(index, forceRescheduleStable: true);
        }
    }

    /// <summary>
    ///     Returns the <see cref="Flight"/> with the matching <paramref name="callsign"/>, or <c>null</c> if no
    ///     matching flight was found.
    /// </summary>
    public Flight? FindFlight(string callsign)
    {
        lock (_gate)
        {
            return _flights
            .FirstOrDefault(f => f.Callsign == callsign);
        }
    }

    /// <summary>
    ///     Returns the index of the provided <paramref name="dateTimeOffset"/> within the sequence.
    /// </summary>
    public int IndexOf(DateTimeOffset dateTimeOffset)
    {
        lock (_gate)
        {
            for (var i = 0; i < _flights.Count; i++)
            {
                if (dateTimeOffset > _flights[i].LandingTime)
                    continue;

                return i;
            }

            return _flights.Count;
        }
    }

    /// <summary>
    ///     Returns the index of the provided <paramref name="flight"/> within the sequence.
    /// </summary>
    public int IndexOf(Flight flight)
    {
        lock (_gate)
        {
            return _flights.IndexOf(flight);
        }
    }

    public int FindIndex(Func<Flight, bool> predicate)
    {
        lock (_gate)
        {
            return _flights.FindIndex(f => predicate(f));
        }
    }

    public int FindIndex(int startIndex, Func<Flight, bool> predicate)
    {
        lock (_gate)
        {
            return _flights.FindIndex(startIndex, f => predicate(f));
        }
    }

    public int FindLastIndex(Func<Flight, bool> predicate)
    {
        lock (_gate)
        {
            return _flights.FindLastIndex(f => predicate(f));
        }
    }

    public int FindLastIndex(int startIndex, Func<Flight, bool> predicate)
    {
        lock (_gate)
        {
            return _flights.FindLastIndex(startIndex, f => predicate(f));
        }
    }

    public void Move(Flight flight, int newIndex, bool forceRescheduleStable = false)
    {
        lock (_gate)
        {
            var currentIndex = _flights.IndexOf(flight);
            if (newIndex == currentIndex)
                return;

            ValidateInsertionBetweenImmovableFlights(newIndex, flight.AssignedRunwayIdentifier);
            if (currentIndex != -1)
            {
                _flights.RemoveAt(currentIndex);

                // Removing the flight will change the index of everything behind it
                if (newIndex > currentIndex)
                    newIndex--;
            }

            _flights.Insert(newIndex, flight);

            var recomputeIndex = Math.Min(newIndex, currentIndex);
            Schedule(recomputeIndex, forceRescheduleStable);
        }
    }

    public void Swap(Flight flight1, Flight flight2)
    {
        lock (_gate)
        {
            var index1 = _flights.IndexOf(flight1);
            if (index1 < 0)
                throw new MaestroException($"{flight1.Callsign} not found");

            var index2 = _flights.IndexOf(flight2);
            if (index2 < 0)
                throw new MaestroException($"{flight1.Callsign} not found");

            // Swap positions
            (_flights[index1],  _flights[index2]) = (_flights[index2], _flights[index1]);

            // Swap landing times and runways
            var landingTime1 = flight1.LandingTime;
            var landingTime2 = flight2.LandingTime;
            var flowControls1 = flight1.FlowControls;
            var runway1 = flight1.AssignedRunwayIdentifier;
            var runway2 = flight2.AssignedRunwayIdentifier;
            var flowControls2 = flight2.FlowControls;

            Schedule(flight1, landingTime2, flowControls2, runway2);
            Schedule(flight2, landingTime1, flowControls1, runway1);

            // No need to re-schedule as we're exchanging two flights that are already scheduled
        }
    }

    /// <summary>
    ///     Removes the <paramref name="flight"/> from the sequence, and recomputes the sequence from the point where
    ///     the flight was removed.
    /// </summary>
    public void Remove(Flight flight)
    {
        lock (_gate)
        {
            var index = _flights.IndexOf(flight);
            if (index == -1)
                throw new MaestroException($"{flight.Callsign} not found");

            _flights.RemoveAt(index);

            Schedule(index, forceRescheduleStable: true);
        }
    }

    public Guid CreateSlot(DateTimeOffset start, DateTimeOffset end, string[] runwayIdentifiers)
    {
        lock (_gate)
        {
            var id = Guid.NewGuid();

            var slot = new Slot(id, start, end, runwayIdentifiers);
            _slots.Add(slot);

            var recomputeIndex = IndexOf(start);
            Schedule(recomputeIndex, forceRescheduleStable: true);

            return id;
        }
    }

    public void ModifySlot(Guid id, DateTimeOffset start, DateTimeOffset end)
    {
        lock (_gate)
        {
            var slot = _slots.FirstOrDefault(s => s.Id == id);
            if (slot is null)
                throw new MaestroException($"Slot {id} not found");

            _slots.Remove(slot);

            var newSlot = new Slot(id, start, end, slot.RunwayIdentifiers);
            _slots.Add(newSlot);

            // BUG: If a flight is scheduled to land after the start time, but estimated to land before it, they need
            //  to be recomputed. Maybe this is okay?
            var rescheduleIndex = IndexOf(start);
            Schedule(rescheduleIndex, forceRescheduleStable: true);
        }
    }

    public void DeleteSlot(Guid id)
    {
        lock (_gate)
        {
            var slot = _slots.FirstOrDefault(s => s.Id == id);
            if (slot is null)
                throw new MaestroException($"Slot {id} not found");

            _slots.Remove(slot);

            var rescheduleIndex = IndexOf(slot.StartTime);
            Schedule(rescheduleIndex, forceRescheduleStable: true);
        }
    }

    List<ISequenceItem> BuildSequence()
    {
        var sequence = new List<ISequenceItem>();

        sequence.AddRange(_flights.Select(f => new FlightSequenceItem(f)));

        foreach (var slot in _slots)
        {
            sequence.Insert(IndexOf(slot.StartTime), new SlotSequenceItem(slot));
        }

        // Current runway always goes first
        sequence.Insert(0, new RunwayModeChangeSequenceItem(CurrentRunwayMode, DateTimeOffset.MinValue, DateTimeOffset.MinValue));
        if (NextRunwayMode is not null && LastLandingTimeForCurrentMode is not null && FirstLandingTimeForNewMode is not null)
        {
            sequence.Insert(
                IndexOf(LastLandingTimeForCurrentMode.Value),
                new RunwayModeChangeSequenceItem(NextRunwayMode, LastLandingTimeForCurrentMode.Value, FirstLandingTimeForNewMode.Value));
        }

        return sequence;

        int IndexOf(DateTimeOffset time)
        {
            for (var i = 0; i < sequence.Count; i++)
            {
                // Compare landing estimate if the flight hasn't been scheduled yet
                if (sequence[i] is FlightSequenceItem flightItem &&
                    flightItem.Flight.LandingTime == DateTimeOffset.MinValue &&
                    time <= flightItem.Flight.LandingEstimate)
                    return i;

                if (time > sequence[i].Time)
                    continue;

                return i;
            }

            return sequence.Count;
        }
    }

    /// <summary>
    ///     Scans the sequence from the <paramref name="startIndex"/>, ensuring all flights are appropriately spaced
    ///     from any slots, runway changes, and other flights on the same or related runways.
    /// </summary>
    /// <param name="forceRescheduleStable">
    ///     When <c>true</c>, flights that are not <see cref="State.Unstable"/> can be displaced (position or landing time changed).
    ///     When <c>false</c>, flights that are not <see cref="State.Unstable"/> will not be affected. Any <see cref="State.Unstable"/> flights
    ///     in conflict with a non-<see cref="State.Unstable"/> flight will be moved behind them as to not adjust their landing times.
    /// </param>
    public void Schedule(int startIndex, bool forceRescheduleStable)
    {
        lock (_gate)
        {
            if (_flights.Count == 0 || startIndex >= _flights.Count)
                return;

            var sequence = BuildSequence();

            var startingFlight =  _flights[startIndex];
            var effectiveStartIndex = sequence.FindIndex(i => i is FlightSequenceItem f && f.Flight == startingFlight);

            for (var i = effectiveStartIndex; i < sequence.Count; i++)
            {
                var currentItem = sequence[i];
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

                var runwayModeItem = sequence
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
                var nextItem = sequence
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
                var precedingItemsOnRunway = sequence
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

                    // Don't move in front of frozen or landed flights
                    else if (previousItem is FlightSequenceItem { Flight.State: State.Frozen or State.Landed })
                    {
                    }

                    // Don't move into a slot
                    else if (previousItem is SlotSequenceItem slotItem &&
                             currentFlight.LandingEstimate.IsAfter(slotItem.Slot.StartTime) &&
                             currentFlight.LandingEstimate.IsBefore(slotItem.Slot.EndTime))
                    {
                    }

                    // Don't move into a runway mode change period
                    else if (previousItem is RunwayModeChangeSequenceItem runwayModeChangeItem &&
                             currentFlight.LandingEstimate.IsAfter(runwayModeChangeItem
                                 .LastLandingTimeInPreviousMode) &&
                             currentFlight.LandingEstimate.IsBefore(runwayModeChangeItem.FirstLandingTimeInNewMode))
                    {
                    }

                    // Previous flight also has maximum delay, if they have an earlier ETA, don't move past them
                    else if (previousItem is FlightSequenceItem { Flight.MaximumDelay: not null } previousFlightItem &&
                             previousFlightItem.Flight.LandingEstimate.IsBefore(currentFlight.LandingEstimate))
                    {
                    }

                    // Delay exceeds the maximum, move this flight forward one space and reprocess
                    else if (totalDelay > currentFlight.MaximumDelay)
                    {
                        var previousItemIndex = sequence.IndexOf(previousItem);
                        if (previousItemIndex != -1)
                        {
                            (sequence[i], sequence[previousItemIndex]) = (sequence[previousItemIndex], sequence[i]);
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
                            var nextItemIndex = sequence.IndexOf(nextItem);
                            if (nextItemIndex == -1)
                            {
                                continue;
                            }

                            (sequence[i], sequence[nextItemIndex]) = (sequence[nextItemIndex], sequence[i]);

                            // Reprocess this index since we've moved something new into this position
                            i--;
                            continue;
                        }
                    }
                }

                // TODO: Double check how this is supposed to work
                FlowControls flowControls;
                if (currentFlight.AircraftCategory == AircraftCategory.Jet && landingTime.IsAfter(currentFlight.LandingEstimate))
                {
                    flowControls = FlowControls.ReduceSpeed;
                }
                else
                {
                    flowControls = FlowControls.ProfileSpeed;
                }

                Schedule(currentFlight, landingTime, flowControls, runway.Identifier);

                // Preserve the order of the sequence with respect to flights on other runways
                if (i < sequence.Count - 1)
                {
                    var nextSequenceItem = sequence[i + 1];
                    if (!AppliesToRunway(nextSequenceItem, runway) && landingTime.IsAfter(nextSequenceItem.Time))
                    {
                        (sequence[i], sequence[i + 1]) = (sequence[i + 1], sequence[i]);
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

            _flights.Clear();
            _flights.AddRange(sequence.OfType<FlightSequenceItem>().Select(f => f.Flight));
        }
    }

    void Schedule(Flight flight, DateTimeOffset landingTime, FlowControls flowControls, string runwayIdentifier)
    {
        flight.SetRunway(runwayIdentifier, manual: flight.RunwayManuallyAssigned);

        DateTimeOffset? feederFixTime = null;
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
                feederFixTime = landingTime.Subtract(arrivalInterval.Value);
            }
        }

        flight.SetSequenceData(landingTime, feederFixTime, flowControls);
    }

    // TODO: Invoke this from handlers rather than internally.
    // The schedule method will prevent frozen flights from being displaced
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
            return _flights
                .Take(insertionIndex)
                .Where(f => f.AssignedRunwayIdentifier == runwayIdentifier)
                .LastOrDefault();
        }

        Flight? GetNextFlightOnRunway()
        {
            return _flights
                .Skip(insertionIndex)
                .Where(f => f.AssignedRunwayIdentifier == runwayIdentifier)
                .FirstOrDefault();
        }
    }

    // TODO: Store these on the flight itself
    public int NumberInSequence(Flight flight) => NumberInSequence(flight.Callsign);

    public int NumberForRunway(Flight flight) => NumberForRunway(flight.Callsign, flight.AssignedRunwayIdentifier);

    int NumberInSequence(string callsign)
    {
        var index = _flights
            .Where(f => f.State is not State.Landed)
            .OrderBy(i => i.LandingTime)
            .ToList()
            .FindIndex(f => f.Callsign == callsign);

        if (index == -1)
            return -1;

        return index + 1;
    }

    int NumberForRunway(string callsign, string runwayIdentifier)
    {
        var index = _flights
            .Where(f => f.State is not State.Landed && f.AssignedRunwayIdentifier == runwayIdentifier)
            .OrderBy(i => i.LandingTime)
            .ToList()
            .FindIndex(f => f.Callsign == callsign);

        if (index == -1)
            return -1;

        return index + 1;
    }

    // TODO: Rename to snapshot
    public SequenceMessage ToMessage()
    {
        return new SequenceMessage
        {
            // AirportIdentifier = AirportIdentifier,
            Flights = _flights
                .Select(f => f.ToMessage(this))
                .ToArray(),
            CurrentRunwayMode = CurrentRunwayMode.ToMessage(),
            NextRunwayMode = NextRunwayMode?.ToMessage(),
            LastLandingTimeForCurrentMode = LastLandingTimeForCurrentMode ?? default,
            FirstLandingTimeForNextMode = FirstLandingTimeForNewMode ?? default,
            Slots = _slots
                .Select(s => s.ToMessage())
                .ToArray()
        };
    }

    public void Restore(SequenceMessage message)
    {
        // Clear existing state
        _flights.Clear();
        _slots.Clear();

        CurrentRunwayMode = new RunwayMode(message.CurrentRunwayMode);
        if (message.NextRunwayMode is not null)
        {
            NextRunwayMode = new RunwayMode(message.NextRunwayMode);
            LastLandingTimeForCurrentMode = message.LastLandingTimeForCurrentMode;
            FirstLandingTimeForNewMode = message.FirstLandingTimeForNextMode;
        }

        // Restore slots
        foreach (var slotMessage in message.Slots)
        {
            var slot = new Slot(slotMessage.Id, slotMessage.StartTime, slotMessage.EndTime, slotMessage.RunwayIdentifiers);
            _slots.Add(slot);
        }

        // Restore sequenced flights (both real and manually-inserted)
        foreach (var flightMessage in message.Flights)
        {
            var flight = new Flight(flightMessage);
            _flights.Add(flight);
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
