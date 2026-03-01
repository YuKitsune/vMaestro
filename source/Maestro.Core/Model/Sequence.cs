using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public class Sequence
{
    readonly object _gate = new();

    readonly AirportConfiguration _airportConfiguration;

    // TODO: Figure out how to get rid of these from here
    readonly ITrajectoryService _trajectoryService;
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

    public Sequence(AirportConfiguration airportConfiguration, ITrajectoryService trajectoryService, IClock clock)
    {
        _airportConfiguration = airportConfiguration;
        _trajectoryService = trajectoryService;
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
            Schedule(0);
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
            Schedule(recomputeIndex);
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

    public void ThrowIsTimeIsUnavailable(string callsign, DateTimeOffset landingTime, string runwayIdentifier)
    {
        lock (_gate)
        {
            if (LastLandingTimeForCurrentMode is not null && FirstLandingTimeForNewMode is not null)
            {
                if (landingTime.IsAfter(LastLandingTimeForCurrentMode.Value) &&
                    landingTime.IsBefore(FirstLandingTimeForNewMode.Value))
                {
                    throw new MaestroException($"Landing time {landingTime:HHmm} is unavailable due to a runway change.");
                }
            }

            var slots = _slots.Where(s => s.RunwayIdentifiers.Contains(runwayIdentifier));
            foreach (var slot in slots)
            {
                if (landingTime.IsSameOrAfter(slot.StartTime) && landingTime.IsSameOrBefore(slot.EndTime))
                {
                    throw new MaestroException($"Landing time {landingTime:HHmm} is unavailable due to a slot.");
                }
            }

            var frozenFlights = _flights.Where(f => f.Callsign != callsign && f.State is State.Frozen or State.Landed);
            foreach (var frozenFlight in frozenFlights)
            {
                if (landingTime == frozenFlight.LandingTime)
                {
                    throw new MaestroException($"Landing time {landingTime:HHmm} is unavailable due to insufficient separation from {frozenFlight.Callsign}.");
                }

                // Can't go in front of frozen flights because we can't delay them, but we can go behind them,
                // hence why we don't check if the landing time is after the frozen flight
                if (!landingTime.IsBefore(frozenFlight.LandingTime))
                    continue;

                // Selected time is in front, determine the required separation based on the frozen flights landing time
                var runwayMode = GetRunwayModeAt(frozenFlight.LandingTime);
                var runway = runwayMode.Runways.FirstOrDefault(f => f.Identifier == frozenFlight.AssignedRunwayIdentifier) ?? runwayMode.Default;

                var requiredSeparation = runway.Identifier == runwayIdentifier
                    ? runway.AcceptanceRate
                    : runwayMode.DependencyRate;
                var actualSeparation = frozenFlight.LandingTime - landingTime;

                if (actualSeparation < requiredSeparation)
                {
                    throw new MaestroException($"Landing time {landingTime:HHmm} is unavailable due to insufficient separation from {frozenFlight.Callsign}.");
                }
            }
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
            Schedule(index);
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

    public void Move(Flight flight, int newIndex)
    {
        lock (_gate)
        {
            var currentIndex = _flights.IndexOf(flight);
            if (newIndex != currentIndex)
            {
                ValidateInsertionBetweenImmovableFlights(newIndex, flight.AssignedRunwayIdentifier);
                if (currentIndex != -1)
                {
                    _flights.RemoveAt(currentIndex);

                    // Removing the flight will change the index of everything behind it
                    if (newIndex > currentIndex)
                        newIndex--;
                }

                _flights.Insert(newIndex, flight);
            }

            var recomputeIndex = Math.Min(newIndex, currentIndex);
            Schedule(recomputeIndex);
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
            var flowControls2 = flight2.FlowControls;
            var runway1 = flight1.AssignedRunwayIdentifier;
            var runway2 = flight2.AssignedRunwayIdentifier;
            var approachType1 = flight1.ApproachType;
            var approachType2 = flight2.ApproachType;

            Schedule(flight1, landingTime2, runway2, approachType2, flowControls2);
            Schedule(flight2, landingTime1, runway1, approachType1, flowControls1);

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

            Schedule(index);
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
            Schedule(recomputeIndex);

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
            Schedule(rescheduleIndex);
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
            Schedule(rescheduleIndex);
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
    public void Schedule(int startIndex)
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

                var runwayModeItem = sequence
                    .Take(i)
                    .OfType<RunwayModeChangeSequenceItem>()
                    .LastOrDefault();
                if (runwayModeItem is null)
                    throw new Exception("No runway mode found");

                var currentRunwayMode = runwayModeItem.RunwayMode;

                // TODO: Consider how we should handle delays into new runway modes
                var runwayOptions = GetRunways(
                    _airportConfiguration,
                    currentFlight,
                    currentRunwayMode);

                // TODO: Use ETA_FF + TTGmin if no runway has been assigned

                var targetLandingTime = currentFlight.TargetLandingTime ?? currentFlight.LandingEstimate;

                // Evaluate each runway option to find the one with the earliest landing time
                var results = runwayOptions
                    .Select(runwayOption => EvaluateRunwayOption(runwayOption, currentFlight, i, targetLandingTime, currentRunwayMode))
                    .ToList();

                // Select the runway option with the earliest landing time
                var result = results
                    .OrderBy(e => e.LandingTime)
                    .First();

                // Move flight to the final position if needed
                if (result.SequencePosition != i)
                {
                    sequence.RemoveAt(i);

                    // Adjust insertion position when moving backwards (to a later position)
                    // because removal shifts all subsequent indices down by 1
                    var insertPosition = result.SequencePosition;
                    if (insertPosition > i)
                        insertPosition--;

                    sequence.Insert(insertPosition, currentItem);

                    // Only adjust loop index when moving forward (to earlier position)
                    // to avoid skipping flights beyond the effective start index
                    if (result.SequencePosition < i)
                    {
                        i = result.SequencePosition - 1;
                    }
                    continue;
                }

                // Assign runway, approach type, landing time, and feeder fix time
                var landingTime = result.LandingTime;

                // TODO: Double check how this is supposed to work
                var flowControls =
                    currentFlight.AircraftCategory == AircraftCategory.Jet &&
                    landingTime.IsAfter(currentFlight.LandingEstimate)
                        ? FlowControls.ReduceSpeed
                        : FlowControls.ProfileSpeed;

                Schedule(currentFlight, landingTime, result.Option.RunwayIdentifier, result.Option.ApproachType, flowControls);
            }

            _flights.Clear();
            _flights.AddRange(sequence.OfType<FlightSequenceItem>().Select(f => f.Flight));

            TimeSpan EffectiveMaximumDelay(TimeSpan maximumDelay, Runway referenceRunway)
            {
                return maximumDelay == TimeSpan.Zero
                    ? referenceRunway.AcceptanceRate
                    : maximumDelay;
            }

            int FindInsertionPointForMaximumDelay(
                int currentIndex,
                DateTimeOffset landingEstimate,
                TimeSpan maximumDelay,
                RunwayMode runwayMode,
                Runway referenceRunway)
            {
                // TODO: What if we move forward into a previous runway mode?

                // Zero-delay flights can be delayed up to the runway acceptance rate
                var effectiveMaximumDelay = EffectiveMaximumDelay(maximumDelay, referenceRunway);

                for (var candidateIndex = currentIndex - 1; candidateIndex > 0; candidateIndex--)
                {
                    var earliestLandingTime = GetEarliestLandingTimeForIndex(candidateIndex, landingEstimate, runwayMode, referenceRunway);
                    var latestLandingTime = GetLatestLandingTimeForIndex(candidateIndex, runwayMode, referenceRunway);

                    // This slot isn't available, try the next one
                    if (latestLandingTime.HasValue && earliestLandingTime.IsAfter(latestLandingTime.Value))
                    {
                        continue;
                    }

                    // Check if we would conflict with the item currently at this position
                    // When we insert here, that item gets displaced to candidateIndex+1
                    // If it's immovable (slot, runway change, frozen flight), we can't push past its time constraint
                    var itemAtPosition = sequence[candidateIndex];
                    var latestFromDisplacedItem = GetLatestLandingTimeFromItem(itemAtPosition, runwayMode, referenceRunway);
                    if (latestFromDisplacedItem.HasValue && earliestLandingTime.IsAfter(latestFromDisplacedItem.Value))
                    {
                        // Would create a conflict with the item we're displacing
                        continue;
                    }

                    var totalDelay = earliestLandingTime - landingEstimate;
                    if (totalDelay > effectiveMaximumDelay)
                    {
                        continue;
                    }

                    return candidateIndex;
                }

                // Can't move any further forward
                return currentIndex;
            }

            DateTimeOffset GetEarliestLandingTimeForIndex(int index, DateTimeOffset landingEstimate, RunwayMode runwayMode, Runway referenceRunway)
            {
                var earliestLandingTime = sequence
                    .Take(index)
                    .Max(s => GetEarliestLandingTimeFromItem(s, runwayMode, referenceRunway));

                if (earliestLandingTime.HasValue && earliestLandingTime.Value.IsAfter(landingEstimate))
                    return earliestLandingTime.Value;

                return landingEstimate;
            }

            DateTimeOffset? GetLatestLandingTimeForIndex(int index, RunwayMode runwayMode, Runway referenceRunway)
            {
                var latestLandingTime = sequence
                    .Skip(index)
                    .Min(s => GetLatestLandingTimeFromItem(s, runwayMode, referenceRunway));

                return latestLandingTime;
            }

            DateTimeOffset? GetLatestLandingTimeFromItem(ISequenceItem item, RunwayMode runwayMode, Runway referenceRunway)
            {
                switch (item)
                {
                    case RunwayModeChangeSequenceItem runwayModeChangeItem:
                        return runwayModeChangeItem.LastLandingTimeInPreviousMode;

                    case SlotSequenceItem slotSequenceItem when slotSequenceItem.Slot.RunwayIdentifiers.Contains(referenceRunway.Identifier):
                        return slotSequenceItem.Slot.StartTime;

                    // Landed and Frozen flights cannot move
                    // Any other flight can be moved, so we'll ignore them and rely on the next iteration to re-calculate their STA
                    case FlightSequenceItem { Flight.State: State.Landed or State.Frozen } flightSequenceItem:
                    {
                        var requiredSeparation = GetRequiredSeparation(flightSequenceItem, referenceRunway, runwayMode);
                        if (requiredSeparation == TimeSpan.Zero)
                            return null;

                        return flightSequenceItem.Flight.LandingTime.Subtract(requiredSeparation);
                    }

                    default: return null;
                }
            }

            DateTimeOffset? GetEarliestLandingTimeFromItem(ISequenceItem item, RunwayMode runwayMode, Runway referenceRunway)
            {
                switch (item)
                {
                    case RunwayModeChangeSequenceItem runwayModeChangeItem:
                        return runwayModeChangeItem.FirstLandingTimeInNewMode;

                    case SlotSequenceItem slotSequenceItem when slotSequenceItem.Slot.RunwayIdentifiers.Contains(referenceRunway.Identifier):
                        return slotSequenceItem.Slot.EndTime;

                    case FlightSequenceItem flightSequenceItem:
                    {
                        var requiredSeparation = GetRequiredSeparation(flightSequenceItem, referenceRunway, runwayMode);
                        if (requiredSeparation == TimeSpan.Zero)
                            return null;

                        return flightSequenceItem.Flight.LandingTime.Add(requiredSeparation);
                    }

                    default: return null;
                }
            }

            TimeSpan GetRequiredSeparation(FlightSequenceItem flightSequenceItem, Runway referenceRunway, RunwayMode runwayMode)
            {
                if (flightSequenceItem.Flight.AssignedRunwayIdentifier == referenceRunway.Identifier)
                {
                    // Same runway, use the acceptance rate
                    return referenceRunway.AcceptanceRate;
                }

                if (runwayMode.Runways.Any(r => r.Identifier == referenceRunway.Identifier))
                {
                    // Same mode, use the dependency rate
                    return runwayMode.DependencyRate;
                }

                // Runway not in mode, use off-mode rate
                return runwayMode.OffModeSeparation;
            }

            (RunwayOption Option, DateTimeOffset LandingTime, int SequencePosition) EvaluateRunwayOption(
                RunwayOption runwayOption,
                Flight flight,
                int currentIndex,
                DateTimeOffset targetLandingTime,
                RunwayMode runwayMode)
            {
                // Create a Runway object from the RunwayOption for use with helper methods
                var runway = new Runway(
                    runwayOption.RunwayIdentifier,
                    runwayOption.ApproachType,
                    runwayOption.RequiredSeparation,
                    []);

                // TODO: Use ETA_FF + TTG for current runway + approach type

                var position = currentIndex;

                // Calculate earliest landing time at current position
                var earliestLandingTime = GetEarliestLandingTimeForIndex(position, targetLandingTime, runwayMode, runway);

                // Check for conflicts with items behind us in the sequence (later positions)
                var latestLandingTime = GetLatestLandingTimeForIndex(position, runwayMode, runway);
                if (latestLandingTime.HasValue && earliestLandingTime.IsAfter(latestLandingTime.Value))
                {
                    // Conflict detected - need to move later in sequence
                    var foundValidPosition = false;
                    for (var candidateIndex = position + 1; candidateIndex <= sequence.Count; candidateIndex++)
                    {
                        var candidateEarliest = GetEarliestLandingTimeForIndex(candidateIndex, targetLandingTime, runwayMode, runway);
                        var candidateLatest = GetLatestLandingTimeForIndex(candidateIndex, runwayMode, runway);

                        // Check if this position is valid
                        if (candidateLatest.HasValue && !candidateEarliest.IsSameOrBefore(candidateLatest.Value))
                            continue;

                        // Also check we don't conflict with the item we're displacing
                        if (candidateIndex < sequence.Count)
                        {
                            var itemAtPosition = sequence[candidateIndex];
                            var latestFromDisplacedItem = GetLatestLandingTimeFromItem(itemAtPosition, runwayMode, runway);
                            if (latestFromDisplacedItem.HasValue && candidateEarliest.IsAfter(latestFromDisplacedItem.Value))
                            {
                                continue;
                            }
                        }

                        position = candidateIndex;
                        earliestLandingTime = candidateEarliest;
                        foundValidPosition = true;
                        break;
                    }

                    // If we couldn't find a valid position backwards, stay at current position
                    if (!foundValidPosition)
                    {
                        position = currentIndex;
                    }
                }

                var landingTime = earliestLandingTime;

                // Check maximum delay constraint
                var landingEstimate = flight.LandingEstimate;
                var totalDelay = landingTime - landingEstimate;
                if (flight.MaximumDelay.HasValue)
                {
                    var effectiveMax = EffectiveMaximumDelay(flight.MaximumDelay.Value, runway);
                    if (totalDelay > effectiveMax)
                    {
                        // Try to find a position forward that respects max delay
                        var newPosition = FindInsertionPointForMaximumDelay(
                            position,
                            landingEstimate,
                            flight.MaximumDelay.Value,
                            runwayMode,
                            runway);

                        if (newPosition < position)
                        {
                            position = newPosition;
                            landingTime = GetEarliestLandingTimeForIndex(newPosition, targetLandingTime, runwayMode, runway);
                        }
                    }
                }

                return (runwayOption, landingTime, position);
            }
        }
    }

    void Schedule(Flight flight, DateTimeOffset landingTime, string runwayIdentifier, string approachType, FlowControls flowControls)
    {
        // Lookup trajectory before setting runway/approach
        var trajectory = _trajectoryService.GetTrajectory(flight, runwayIdentifier, approachType);

        // Atomic update: runway + trajectory + ETA + STA_FF
        flight.SetRunway(runwayIdentifier, trajectory);
        flight.SetApproachType(approachType, trajectory);

        flight.SetSequenceData(landingTime, flowControls);
    }

    record RunwayOption(string RunwayIdentifier, string ApproachType, TimeSpan RequiredSeparation);

    // TODO: Extract this out into a separate service so we can test it
    RunwayOption[] GetRunways(AirportConfiguration airportConfiguration, Flight flight, RunwayMode runwayMode)
    {
        // If a runway is assigned, and the flight is stable, leave it as-is
        // For stable flights, preserve both the runway AND the approach type
        if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier) && flight.State is not State.Unstable)
        {
            var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier);
            if (runway is not null)
            {
                return [new RunwayOption(runway.Identifier, flight.ApproachType, runway.AcceptanceRate)];
            }

            // Runway is off-mode, create an ad-hoc option
            var separation = runwayMode.OffModeSeparation == TimeSpan.Zero
                ? TimeSpan.FromSeconds(airportConfiguration.DefaultOffModeSeparationSeconds)
                : runwayMode.OffModeSeparation;

            return [new RunwayOption(flight.AssignedRunwayIdentifier, flight.ApproachType, separation)];
        }

        var possibleRunways = new HashSet<RunwayOption>();
        foreach (var runway in runwayMode.Runways)
        {
            // If the runway requires a feeder fix to match, and this aircraft is tracking via that fix, we can assign it
            if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && runway.FeederFixes.Contains(flight.FeederFixIdentifier))
            {
                possibleRunways.Add(new RunwayOption(runway.Identifier, runway.ApproachType, runway.AcceptanceRate));
            }

            // Runway has no specific feeder fix requirements, so we can assign it regardless of the feeder
            if (!runway.FeederFixes.Any())
            {
                possibleRunways.Add(new RunwayOption(runway.Identifier, runway.ApproachType, runway.AcceptanceRate));
            }
        }

        if (possibleRunways.Count == 0)
        {
            // Couldn't find a good match, just use the default
            possibleRunways.Add(new RunwayOption(runwayMode.Default.Identifier, runwayMode.Default.ApproachType, runwayMode.Default.AcceptanceRate));
        }

        return possibleRunways.ToArray();
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
