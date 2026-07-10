using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using Serilog;

namespace Maestro.Core.Model;

public interface IConfigurationChange;

public record TerminalConfigurationChange(
    RunwayMode NewRunwayMode,
    DateTimeOffset LastLandingTimeInPreviousMode,
    DateTimeOffset FirstLandingTimeInNewMode) : IConfigurationChange;

public record LandingRatesChange(
    IReadOnlyDictionary<string, TimeSpan> NewLandingRates,
    DateTimeOffset ChangeTime) : IConfigurationChange;

public class Sequence
{
    readonly object _gate = new();

    readonly AirportConfiguration _airportConfiguration;

    // TODO: Figure out how to get rid of these from here
    readonly ITrajectoryService _trajectoryService;
    readonly IClock _clock;
    readonly ILogger _logger;

    readonly List<Slot> _slots = [];
    readonly List<Flight> _flights = [];

    public string AirportIdentifier { get; }

    public IReadOnlyList<Slot> Slots => _slots.AsReadOnly();
    public IReadOnlyList<Flight> Flights => _flights.AsReadOnly();

    public RunwayMode CurrentRunwayMode { get; private set; }
    public IConfigurationChange? PendingConfigurationChange { get; private set; }

    public Wind SurfaceWind { get; set; } = new(0, 0);
    public Wind UpperWind { get; set; } = new(0, 0);
    public int UpperWindAltitude { get; }
    public bool ManualWind { get; set; }

    public Sequence(AirportConfiguration airportConfiguration, ITrajectoryService trajectoryService, IClock clock, ILogger logger)
    {
        _airportConfiguration = airportConfiguration;
        _trajectoryService = trajectoryService;
        _clock = clock;
        _logger = logger
            .ForContext<Sequence>()
            .ForContext("AirportIdentifier", airportConfiguration.Identifier);

        AirportIdentifier = airportConfiguration.Identifier;
        CurrentRunwayMode = new RunwayMode(
            airportConfiguration.RunwayModes.First(),
            TimeSpan.FromSeconds(airportConfiguration.DefaultOffModeSeparationSeconds));

        UpperWindAltitude = airportConfiguration.UpperWindAltitude;
    }

    /// <summary>
    ///     Immediately changes the runway mode and recomputes the entire sequence.
    /// </summary>
    public void ChangeRunwayMode(RunwayMode runwayMode)
    {
        lock (_gate)
        {
            CurrentRunwayMode = runwayMode;

            ResetRunwayAssignmentsFrom(0);
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
            var recomputeBoundary = PendingConfigurationChange is TerminalConfigurationChange terminalConfigurationChange
                ? DateTimeOffsetHelpers.Earliest(terminalConfigurationChange.LastLandingTimeInPreviousMode, lastLandingTimeForOldMode)
                : lastLandingTimeForOldMode;

            PendingConfigurationChange = new TerminalConfigurationChange(
                runwayMode,
                lastLandingTimeForOldMode,
                firstLandingTimeForNewMode);

            var recomputeIndex = IndexOf(recomputeBoundary);

            ResetRunwayAssignmentsFrom(recomputeIndex);
            Schedule(recomputeIndex, forceRescheduleStable: true);
        }
    }

    public void ChangeLandingRates(IReadOnlyDictionary<string, TimeSpan> newLandingRates, DateTimeOffset ratesChangeTime)
    {
        lock (_gate)
        {
            // Rates can only be provided for runways in the current runway mode
            var currentRunways = CurrentRunwayMode.Runways.Select(x => x.Identifier).ToHashSet();
            if (!newLandingRates.Keys.All(currentRunways.Contains))
            {
                throw new MaestroException("Provided landing rates are not valid for the current runway mode");
            }

            PendingConfigurationChange = new LandingRatesChange(newLandingRates, ratesChangeTime);

            var recomputeIndex = IndexOf(ratesChangeTime);
            Schedule(recomputeIndex, forceRescheduleStable: true);
        }
    }

    public void CancelConfigurationChange()
    {
        lock (_gate)
        {
            if (PendingConfigurationChange is null)
                return;

            var recomputeTime = PendingConfigurationChange switch
            {
                TerminalConfigurationChange terminalConfigurationChange => terminalConfigurationChange.FirstLandingTimeInNewMode,
                LandingRatesChange landingRatesChange => landingRatesChange.ChangeTime,
                _ => throw new ArgumentOutOfRangeException()
            };

            var recomputeIndex = IndexOf(recomputeTime);

            PendingConfigurationChange = null;

            Schedule(recomputeIndex, forceRescheduleStable: true);
        }
    }

    public bool TrySwapTerminalConfiguration()
    {
        lock (_gate)
        {
            if (PendingConfigurationChange is null)
                return false;

            var changeTime = PendingConfigurationChange switch
            {
                TerminalConfigurationChange terminalConfigurationChange => terminalConfigurationChange.FirstLandingTimeInNewMode,
                LandingRatesChange landingRatesChange => landingRatesChange.ChangeTime,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (_clock.UtcNow().IsBefore(changeTime))
                return false;

            var nextMode = PendingConfigurationChange switch
            {
                TerminalConfigurationChange terminalConfigurationChange => terminalConfigurationChange.NewRunwayMode,
                LandingRatesChange landingRatesChange => CurrentRunwayMode.WithLandingRates(landingRatesChange.NewLandingRates),
                _ => throw new ArgumentOutOfRangeException()
            };

            CurrentRunwayMode = nextMode;
            PendingConfigurationChange = null;

            return true;
        }
    }

    /// <summary>
    ///     Returns the active <see cref="RunwayMode"/> at the specified <paramref name="time"/>.
    /// </summary>
    public RunwayMode GetRunwayModeAt(DateTimeOffset time)
    {
        lock (_gate)
        {
            if (PendingConfigurationChange is null)
                return CurrentRunwayMode;

            var changeTime = PendingConfigurationChange switch
            {
                TerminalConfigurationChange terminalConfigurationChange => terminalConfigurationChange.FirstLandingTimeInNewMode,
                LandingRatesChange landingRatesChange => landingRatesChange.ChangeTime,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (time.IsBefore(changeTime))
                return CurrentRunwayMode;

            var nextMode = PendingConfigurationChange switch
            {
                TerminalConfigurationChange terminalConfigurationChange => terminalConfigurationChange.NewRunwayMode,
                LandingRatesChange landingRatesChange => CurrentRunwayMode.WithLandingRates(landingRatesChange.NewLandingRates),
                _ => throw new ArgumentOutOfRangeException()
            };

            return nextMode;
        }
    }

    public void ThrowIsTimeIsUnavailable(string callsign, DateTimeOffset landingTime, string runwayIdentifier)
    {
        lock (_gate)
        {
            if (PendingConfigurationChange is TerminalConfigurationChange terminalConfigurationChange)
            {
                if (landingTime.IsAfter(terminalConfigurationChange.LastLandingTimeInPreviousMode) &&
                    landingTime.IsBefore(terminalConfigurationChange.FirstLandingTimeInNewMode))
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
            return _flights.FirstOrDefault(f => f.Callsign == callsign);
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
            var runway1 = flight1.RunwayAssignment;
            var runway2 = flight2.RunwayAssignment;
            var approachType1 = flight1.ApproachType;
            var approachType2 = flight2.ApproachType;

            var trajectory1 = _trajectoryService.GetTrajectory(
                flight1,
                runway2.RunwayIdentifier,
                approachType2,
                [], // TODO
                UpperWind);

            var trajectory2 = _trajectoryService.GetTrajectory(
                flight2,
                runway1.RunwayIdentifier,
                approachType1,
                [], // TODO
                UpperWind);

            Schedule(flight1, landingTime2, runway2, approachType2, trajectory1);
            Schedule(flight2, landingTime1, runway1, approachType1, trajectory2);

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
        switch (PendingConfigurationChange)
        {
            case TerminalConfigurationChange terminalConfigurationChange:
                sequence.Insert(
                    IndexOf(terminalConfigurationChange.LastLandingTimeInPreviousMode),
                    new RunwayModeChangeSequenceItem(
                        terminalConfigurationChange.NewRunwayMode,
                        terminalConfigurationChange.LastLandingTimeInPreviousMode,
                        terminalConfigurationChange.FirstLandingTimeInNewMode));
                break;

            case LandingRatesChange landingRatesChange:
                sequence.Insert(
                    IndexOf(landingRatesChange.ChangeTime),
                    new RunwayModeChangeSequenceItem(
                        CurrentRunwayMode.WithLandingRates(landingRatesChange.NewLandingRates),
                        landingRatesChange.ChangeTime,
                        landingRatesChange.ChangeTime));
                break;
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
    ///     Resets manual runway assignments to automatic runway assignments for flights landing from <paramref name="startIndex"/> onwards.
    /// </summary>
    void ResetRunwayAssignmentsFrom(int startIndex)
    {
        for (var i = startIndex; i < _flights.Count; i++)
        {
            if (_flights[i].RunwayAssignment is ManualRunwayAssignment manualRunwayAssignment)
            {
                _flights[i].SetRunway(
                    new AutomaticRunwayAssignment(manualRunwayAssignment.RunwayIdentifier),
                    _flights[i].TerminalTrajectory);
            }
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
    public void Schedule(int startIndex, bool forceRescheduleStable = false)
    {
        lock (_gate)
        {
            if (_flights.Count == 0 || startIndex >= _flights.Count)
                return;

            var sequence = BuildSequence();

            var startingFlight =  _flights[startIndex];
            var effectiveStartIndex = sequence.FindIndex(i => i is FlightSequenceItem f && f.Flight == startingFlight);

            var log = _logger.ForContext("ScheduleFrom", startingFlight.Callsign);

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
                if (!forceRescheduleStable && currentFlight.State is State.Stable or State.SuperStable)
                {
                    continue;
                }

                log.Verbose("Scheduling {Callsign} (i={Index})", currentFlight.Callsign, i);

                var runwayModeItem = sequence
                    .Take(i)
                    .OfType<RunwayModeChangeSequenceItem>()
                    .LastOrDefault();
                if (runwayModeItem is null)
                    throw new Exception("No runway mode found");

                var currentRunwayMode = runwayModeItem.RunwayMode;

                log.Verbose("Schedule {Callsign}: Current Runway Mode is {RunwayMode}", currentFlight.Callsign, currentRunwayMode);

                // A flight's available runways depend on the runway mode at its landing position.
                // The backward search in EvaluateRunwayOption can delay a flight past a mode change
                // boundary, so re-evaluate against the mode at the resulting position until it
                // stabilises. Manual assignments are locked and never change runway.
                var schedulingMode = currentRunwayMode;
                (RunwayOption Option, DateTimeOffset LandingTime, int SequencePosition) result;
                for (var attempt = 0; ; attempt++)
                {
                    var runwayOptions = GetRunways(_airportConfiguration, currentFlight, schedulingMode);

                    log.Verbose("Schedule {Callsign}: {Count} runway options found", currentFlight.Callsign, runwayOptions.Length);

                    // For each option, calculate the earliest landing time using the trajectory for that specific runway
                    var results = runwayOptions
                        .Select(runwayOption =>
                        {
                            var targetLandingTime = CalculateTargetLandingTime(currentFlight, runwayOption);
                            return EvaluateRunwayOption(runwayOption, currentFlight, i, targetLandingTime, schedulingMode);
                        })
                        .ToList();

                    // Prefer the earliest landing time, keeping the current runway on a tie to avoid needless reassignment
                    result = results
                        .OrderBy(e => e.LandingTime)
                        .ThenByDescending(e => e.Option.RunwayIdentifier == currentFlight.AssignedRunwayIdentifier)
                        .First();

                    if (results.Count > 1)
                        log.Verbose(
                            "{Callsign} selected RWY {Runway} (earliest STA {LandingTime:HHmm} of {Count} options)",
                            currentFlight.Callsign, result.Option.RunwayIdentifier, result.LandingTime, results.Count);

                    // If the flight was delayed into a different runway mode, re-evaluate its runway
                    // options against that mode so it lands on a compliant runway. Manual assignments
                    // are locked and must not be reassigned.
                    var modeAtResult = GetRunwayModeAtIndex(result.SequencePosition);
                    if (currentFlight.RunwayAssignment is ManualRunwayAssignment
                        || ReferenceEquals(modeAtResult, schedulingMode)
                        || attempt >= 2)
                        break;

                    log.Verbose(
                        "{Callsign} delayed into runway mode {RunwayMode}, re-evaluating runway options",
                        currentFlight.Callsign, modeAtResult.Identifier);
                    schedulingMode = modeAtResult;
                }

                // Move flight to the final position if needed
                if (result.SequencePosition != i)
                {
                    log.Verbose(
                        "{Callsign} repositioned from i={OldIndex} to i={NewIndex} (RWY {Runway}, STA {LandingTime:HHmm})",
                        currentFlight.Callsign, i, result.SequencePosition, result.Option.RunwayIdentifier, result.LandingTime);

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
                        log.Verbose("Moving forward to {NewIndex} to re-schedule {Callsign}", i, currentFlight.Callsign);
                        i = result.SequencePosition - 1;
                    }
                    continue;
                }

                // Assign runway, approach type, landing time, and feeder fix time
                var landingTime = result.LandingTime;

                log.Verbose(
                    "{Callsign} STA {LandingTime:HHmm} (ETA {Estimate:HHmm}, delay {Delay})",
                    currentFlight.Callsign,
                    landingTime,
                    currentFlight.LandingEstimate,
                    landingTime - currentFlight.LandingEstimate);

                IRunwayAssignment runwayAssignment = currentFlight.RunwayAssignment is ManualRunwayAssignment
                    ? new ManualRunwayAssignment(result.Option.RunwayIdentifier)
                    : new AutomaticRunwayAssignment(result.Option.RunwayIdentifier);

                Schedule(currentFlight, landingTime, runwayAssignment, result.Option.ApproachType, result.Option.Trajectory);
            }

            _flights.Clear();
            _flights.AddRange(
                sequence.OfType<FlightSequenceItem>()
                    .Select(f => f.Flight)
                    .OrderBy(f => f.LandingTime));

            TimeSpan EffectiveMaximumDelay(TimeSpan maximumDelay, Runway referenceRunway)
            {
                return maximumDelay == TimeSpan.Zero
                    ? referenceRunway.AcceptanceRate
                    : maximumDelay;
            }

            int FindInsertionPointForMaximumDelay(
                string callsign,
                int currentIndex,
                DateTimeOffset landingEstimate,
                TimeSpan maximumDelay,
                RunwayMode runwayMode,
                Runway referenceRunway)
            {
                // TODO: What if we move forward into a previous runway mode?

                // Zero-delay flights can be delayed up to the runway acceptance rate
                var effectiveMaximumDelay = EffectiveMaximumDelay(maximumDelay, referenceRunway);

                log.Verbose(
                    "  {Callsign} FindInsertionPoint RWY {Runway}: searching from i={From} toward i=0, max delay {Max}",
                    callsign, referenceRunway.Identifier, currentIndex - 1, effectiveMaximumDelay);

                for (var candidateIndex = currentIndex - 1; candidateIndex > 0; candidateIndex--)
                {
                    var earliestLandingTime = GetEarliestLandingTimeForIndex(callsign, candidateIndex, landingEstimate, runwayMode, referenceRunway);
                    var latestLandingTime = GetLatestLandingTimeForIndex(callsign, candidateIndex, runwayMode, referenceRunway);

                    // This slot isn't available, try the next one
                    if (latestLandingTime.HasValue && earliestLandingTime.IsAfter(latestLandingTime.Value))
                    {
                        log.Verbose(
                            "  {Callsign} FindInsertionPoint i={Index}: slot unavailable — earliest {Earliest:HHmm} > latest {Latest:HHmm}",
                            callsign, candidateIndex, earliestLandingTime, latestLandingTime.Value);
                        continue;
                    }

                    // Check if we would conflict with the item currently at this position
                    // When we insert here, that item gets displaced to candidateIndex+1
                    // If it's immovable (slot, runway change, frozen flight), we can't push past its time constraint
                    var itemAtPosition = sequence[candidateIndex];
                    var latestFromDisplacedItem = GetLatestLandingTimeFromItem(itemAtPosition, runwayMode, referenceRunway);
                    if (latestFromDisplacedItem.HasValue && earliestLandingTime.IsAfter(latestFromDisplacedItem.Value))
                    {
                        log.Verbose(
                            "  {Callsign} FindInsertionPoint i={Index}: would displace {Item} (max {Max:HHmm}), skipping",
                            callsign, candidateIndex, DescribeItem(itemAtPosition), latestFromDisplacedItem.Value);
                        continue;
                    }

                    var totalDelay = earliestLandingTime - landingEstimate;
                    if (totalDelay > effectiveMaximumDelay)
                    {
                        log.Verbose(
                            "  {Callsign} FindInsertionPoint i={Index}: delay {Delay} exceeds max {Max}, skipping",
                            callsign, candidateIndex, totalDelay, effectiveMaximumDelay);
                        continue;
                    }

                    log.Verbose(
                        "  {Callsign} FindInsertionPoint i={Index}: valid — earliest {Earliest:HHmm}, delay {Delay}",
                        callsign, candidateIndex, earliestLandingTime, totalDelay);
                    return candidateIndex;
                }

                // Can't move any further forward
                log.Verbose("  {Callsign} FindInsertionPoint: no valid earlier position found, staying at i={Index}", callsign, currentIndex);
                return currentIndex;
            }

            RunwayMode GetRunwayModeAtIndex(int index) =>
                sequence.Take(index)
                    .OfType<RunwayModeChangeSequenceItem>()
                    .LastOrDefault()
                    ?.RunwayMode ?? throw new Exception("No runway mode found");

            DateTimeOffset GetEarliestLandingTimeForIndex(string callsign, int index, DateTimeOffset landingEstimate, RunwayMode runwayMode, Runway referenceRunway)
            {
                ISequenceItem? constraintSource = null;
                DateTimeOffset? maxTime = null;
                foreach (var item in sequence.Take(index))
                {
                    var t = GetEarliestLandingTimeFromItem(item, runwayMode, referenceRunway);
                    if (t.HasValue && (!maxTime.HasValue || t.Value.IsAfter(maxTime.Value)))
                    {
                        maxTime = t;
                        constraintSource = item;
                    }
                }

                if (maxTime.HasValue && maxTime.Value.IsAfter(landingEstimate))
                {
                    log.Verbose(
                        "  {Callsign} Earliest[i={Index}] RWY {Runway}: {Time:HHmm} — after {Constraint}",
                        callsign, index, referenceRunway.Identifier, maxTime.Value, DescribeItem(constraintSource!));
                    return maxTime.Value;
                }

                log.Verbose(
                    "  {Callsign} Earliest[i={Index}] RWY {Runway}: {Time:HHmm} — unconstrained (ETA)",
                    callsign, index, referenceRunway.Identifier, landingEstimate);
                return landingEstimate;
            }

            DateTimeOffset? GetLatestLandingTimeForIndex(string callsign, int index, RunwayMode runwayMode, Runway referenceRunway)
            {
                ISequenceItem? constraintSource = null;
                DateTimeOffset? minTime = null;
                foreach (var item in sequence.Skip(index))
                {
                    var t = GetLatestLandingTimeFromItem(item, runwayMode, referenceRunway);
                    if (t.HasValue && (!minTime.HasValue || t.Value.IsBefore(minTime.Value)))
                    {
                        minTime = t;
                        constraintSource = item;
                    }
                }

                if (minTime.HasValue)
                    log.Verbose(
                        "  {Callsign} Latest[i={Index}] RWY {Runway}: {Time:HHmm} — before {Constraint}",
                        callsign, index, referenceRunway.Identifier, minTime.Value, DescribeItem(constraintSource!));
                else
                    log.Verbose(
                        "  {Callsign} Latest[i={Index}] RWY {Runway}: none",
                        callsign, index, referenceRunway.Identifier);

                return minTime;
            }

            DateTimeOffset? GetLatestLandingTimeFromItem(ISequenceItem item, RunwayMode runwayMode, Runway referenceRunway)
            {
                switch (item)
                {
                    case RunwayModeChangeSequenceItem runwayModeChangeItem:
                        return runwayModeChangeItem.LastLandingTimeInPreviousMode;

                    case SlotSequenceItem slotSequenceItem when slotSequenceItem.Slot.RunwayIdentifiers.Contains(referenceRunway.Identifier):
                        return slotSequenceItem.Slot.StartTime;

                    // Landed and Frozen flights cannot move.
                    // Stable and SuperStable flights cannot move unless forced.
                    // Any other flight can be moved, so we'll ignore them and rely on the next iteration to re-calculate their STA
                    case FlightSequenceItem flightSequenceItem when IsImmovable(flightSequenceItem.Flight):
                    {
                        var requiredSeparation = GetRequiredSeparation(flightSequenceItem, referenceRunway, runwayMode);
                        if (requiredSeparation == TimeSpan.Zero)
                            return null;

                        return flightSequenceItem.Flight.LandingTime.Subtract(requiredSeparation);
                    }

                    default: return null;
                }
            }

            bool IsImmovable(Flight flight)
            {
                if (flight.State is State.Landed or State.Frozen)
                    return true;

                if (!forceRescheduleStable && flight.State is State.Stable or State.SuperStable)
                    return true;

                return false;
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
                var otherRunwayIdentifier = flightSequenceItem.Flight.AssignedRunwayIdentifier;

                if (otherRunwayIdentifier == referenceRunway.Identifier)
                {
                    // Same runway, use the acceptance rate
                    return referenceRunway.AcceptanceRate;
                }

                var referenceInMode = runwayMode.Runways.Any(r => r.Identifier == referenceRunway.Identifier);
                var otherInMode = runwayMode.Runways.Any(r => r.Identifier == otherRunwayIdentifier);

                // A flight on an off-mode runway must be separated from all other flights by the
                // off-mode rate, regardless of which flight is being scheduled.
                if (!referenceInMode || !otherInMode)
                {
                    return runwayMode.OffModeSeparation;
                }

                // Both runways are in the current mode, use the dependency rate
                return runwayMode.DependencyRate;
            }

            DateTimeOffset CalculateTargetLandingTime(Flight currentFlight, RunwayOption runwayOption)
            {
                DateTimeOffset targetLandingTime;
                if (currentFlight.TargetLandingTime.HasValue)
                {
                    targetLandingTime = currentFlight.TargetLandingTime.Value;
                }
                else if (!string.IsNullOrEmpty(currentFlight.FeederFixIdentifier))
                {
                    // Calculate target as FF + TTG for this runway
                    targetLandingTime = currentFlight.FeederFixEstimate.Add(runwayOption.Trajectory.NormalTimeToGo);
                }
                else
                {
                    // No feeder fix, use current landing estimate
                    targetLandingTime = currentFlight.LandingEstimate;
                }

                return targetLandingTime;
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
                var earliestLandingTime = GetEarliestLandingTimeForIndex(flight.Callsign, position, targetLandingTime, runwayMode, runway);

                // Check for conflicts with items backward in the sequence (later positions)
                var latestLandingTime = GetLatestLandingTimeForIndex(flight.Callsign, position, runwayMode, runway);
                if (latestLandingTime.HasValue && earliestLandingTime.IsAfter(latestLandingTime.Value))
                {
                    log.Verbose(
                        "  {Callsign} RWY {Runway}: conflict at i={Index} with the proceeding entry. Earliest {Earliest:HHmm} > latest {Latest:HHmm}, searching backward",
                        flight.Callsign, runwayOption.RunwayIdentifier, position, earliestLandingTime, latestLandingTime.Value);

                    // Conflict detected - need to move backward in sequence
                    var foundValidPosition = false;
                    for (var candidateIndex = position + 1; candidateIndex <= sequence.Count; candidateIndex++)
                    {
                        // The runway mode may change as we search backward past a mode change item.
                        // Use the mode at the candidate position so separation is computed correctly.
                        var candidateRunwayMode = GetRunwayModeAtIndex(candidateIndex);
                        var candidateEarliest = GetEarliestLandingTimeForIndex(flight.Callsign, candidateIndex, targetLandingTime, candidateRunwayMode, runway);
                        var candidateLatest = GetLatestLandingTimeForIndex(flight.Callsign, candidateIndex, candidateRunwayMode, runway);

                        // Check if this position is valid
                        if (candidateLatest.HasValue && !candidateEarliest.IsSameOrBefore(candidateLatest.Value))
                        {
                            log.Verbose(
                                "  {Callsign} RWY {Runway}: candidate i={Index} invalid — earliest {Earliest:HHmm} > latest {Latest:HHmm}",
                                flight.Callsign, runwayOption.RunwayIdentifier, candidateIndex, candidateEarliest, candidateLatest.Value);
                            continue;
                        }

                        // Also check we don't conflict with the item we're displacing
                        if (candidateIndex < sequence.Count)
                        {
                            var itemAtPosition = sequence[candidateIndex];
                            var latestFromDisplacedItem = GetLatestLandingTimeFromItem(itemAtPosition, candidateRunwayMode, runway);
                            if (latestFromDisplacedItem.HasValue && candidateEarliest.IsAfter(latestFromDisplacedItem.Value))
                            {
                                log.Verbose(
                                    "  {Callsign} RWY {Runway}: candidate i={Index} would displace {Item} (max {Max:HHmm}), skipping",
                                    flight.Callsign, runwayOption.RunwayIdentifier, candidateIndex, DescribeItem(itemAtPosition), latestFromDisplacedItem.Value);
                                continue;
                            }
                        }

                        log.Verbose(
                            "  {Callsign} RWY {Runway}: conflict resolved at i={Index}, earliest {Earliest:HHmm}",
                            flight.Callsign, runwayOption.RunwayIdentifier, candidateIndex, candidateEarliest);

                        position = candidateIndex;
                        earliestLandingTime = candidateEarliest;
                        foundValidPosition = true;
                        break;
                    }

                    // If we couldn't find a valid position backward, stay at current position
                    if (!foundValidPosition)
                    {
                        log.Verbose(
                            "  {Callsign} RWY {Runway}: no valid backward position found, staying at i={Index}",
                            flight.Callsign, runwayOption.RunwayIdentifier, currentIndex);
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
                        log.Verbose(
                            "  {Callsign} RWY {Runway}: delay {Delay} exceeds max {Max}, searching earlier",
                            flight.Callsign, runwayOption.RunwayIdentifier, totalDelay, effectiveMax);

                        // Try to find a position forward that respects max delay
                        var newPosition = FindInsertionPointForMaximumDelay(
                            flight.Callsign,
                            position,
                            landingEstimate,
                            flight.MaximumDelay.Value,
                            runwayMode,
                            runway);

                        if (newPosition < position)
                        {
                            landingTime = GetEarliestLandingTimeForIndex(flight.Callsign, newPosition, targetLandingTime, runwayMode, runway);
                            log.Verbose(
                                "  {Callsign} RWY {Runway}: max delay — moved from i={OldIndex} to i={NewIndex}, STA {LandingTime:HHmm}",
                                flight.Callsign, runwayOption.RunwayIdentifier, position, newPosition, landingTime);
                            position = newPosition;
                        }
                        else
                        {
                            log.Verbose(
                                "  {Callsign} RWY {Runway}: max delay exceeded but no earlier position available",
                                flight.Callsign, runwayOption.RunwayIdentifier);
                        }
                    }
                }

                log.Verbose(
                    "  {Callsign} RWY {Runway} evaluated: i={Index}, STA {LandingTime:HHmm}",
                    flight.Callsign, runwayOption.RunwayIdentifier, position, landingTime);

                return (runwayOption, landingTime, position);
            }

            string DescribeItem(ISequenceItem item) => item switch
            {
                RunwayModeChangeSequenceItem rm => $"RWY mode change to {rm.RunwayMode.Identifier}",
                SlotSequenceItem s => $"slot [{s.Slot.StartTime:HHmm}-{s.Slot.EndTime:HHmm}]",
                FlightSequenceItem f => $"{f.Flight.Callsign} ({f.Flight.State}) STA {f.Flight.LandingTime:HHmm} on {f.Flight.AssignedRunwayIdentifier}",
                _ => item.GetType().Name
            };
        }
    }

    void Schedule(
        Flight flight,
        DateTimeOffset landingTime,
        IRunwayAssignment runwayAssignment,
        string approachType,
        TerminalTrajectory trajectory)
    {
        // Atomic update: runway + trajectory + ETA + STA_FF
        flight.SetRunway(runwayAssignment, trajectory);
        flight.SetApproachType(approachType, trajectory);

        // Compute delay distribution and derive control action
        var totalDelay = landingTime - flight.LandingEstimate;

        var distribution = DelayStrategyCalculator.Compute(
            totalDelay,
            trajectory,
            flight.EnrouteTrajectory,
            _airportConfiguration.DelayStrategy);

        var feederFixTime = flight.FeederFixEstimate.Add(distribution.EnrouteDelay);

        _logger.Verbose(
            "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
            flight.Callsign,
            runwayAssignment.RunwayIdentifier,
            approachType,
            trajectory.NormalTimeToGo,
            trajectory.PressureTimeToGo,
            trajectory.MaxPressureTimeToGo);

        flight.SetSequenceData(
            landingTime,
            feederFixTime,
            distribution.ControlAction,
            distribution.EnrouteDelay,
            distribution.TerminalDelay);
    }

    record RunwayOption(string RunwayIdentifier, string ApproachType, TimeSpan RequiredSeparation, TerminalTrajectory Trajectory);

    // TODO: Extract this out into a separate service so we can test it
    RunwayOption[] GetRunways(AirportConfiguration airportConfiguration, Flight flight, RunwayMode runwayMode)
    {
        // Manual assignments are locked: the controller chose the runway and the algorithm must not change it.
        // If the runway is off-mode, use the off-mode separation rate; otherwise the runway's acceptance rate.
        if (flight.RunwayAssignment is ManualRunwayAssignment manualRunwayAssignment)
        {
            var manualRunway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == manualRunwayAssignment.RunwayIdentifier);
            var separation = manualRunway?.AcceptanceRate ?? runwayMode.OffModeSeparation;
            var trajectory = _trajectoryService.GetTrajectory(flight, manualRunwayAssignment.RunwayIdentifier, flight.ApproachType, [], UpperWind);
            return [new RunwayOption(manualRunwayAssignment.RunwayIdentifier, flight.ApproachType, separation, trajectory)];
        }

        // Automatic assignments are free to be reassigned to any valid runway in the mode.
        var possibleRunways = new HashSet<RunwayOption>();
        foreach (var runway in runwayMode.Runways)
        {
            // Preserve a customised approach type when keeping the flight on its current runway;
            // otherwise use the runway's default approach type.
            var approachType = runway.Identifier == flight.AssignedRunwayIdentifier
                ? flight.ApproachType
                : runway.ApproachType;

            // If the runway requires a feeder fix to match, and this aircraft is tracking via that fix, we can assign it
            if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && runway.FeederFixes.Contains(flight.FeederFixIdentifier))
            {
                var trajectory = _trajectoryService.GetTrajectory(flight, runway.Identifier, approachType, [], UpperWind);
                possibleRunways.Add(new RunwayOption(runway.Identifier, approachType, runway.AcceptanceRate, trajectory));
            }

            // Runway has no specific feeder fix requirements, so we can assign it regardless of the feeder
            if (!runway.FeederFixes.Any())
            {
                var trajectory = _trajectoryService.GetTrajectory(flight, runway.Identifier, approachType, [], UpperWind);
                possibleRunways.Add(new RunwayOption(runway.Identifier, approachType, runway.AcceptanceRate, trajectory));
            }
        }

        if (possibleRunways.Count == 0)
        {
            // Couldn't find a good match, just use the default
            _logger.Verbose(
                "{Callsign}: no runway match for feeder fix {FeederFix}, using default {Default}",
                flight.Callsign, flight.FeederFixIdentifier, runwayMode.Default.Identifier);
            var defaultTrajectory = _trajectoryService.GetTrajectory(flight, runwayMode.Default.Identifier, runwayMode.Default.ApproachType, [], UpperWind);
            possibleRunways.Add(new RunwayOption(runwayMode.Default.Identifier, runwayMode.Default.ApproachType, runwayMode.Default.AcceptanceRate, defaultTrajectory));
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
        lock (_gate)
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
    }

    int NumberForRunway(string callsign, string runwayIdentifier)
    {
        lock (_gate)
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
    }

    public SequenceDto ToDto()
    {
        lock (_gate)
        {
            return new SequenceDto
            {
                Flights = _flights
                    .Select(f => f.ToDto(this))
                    .ToArray(),
                CurrentRunwayMode = CurrentRunwayMode.ToDto(),

                PendingConfigurationChange = PendingConfigurationChange switch
                {
                    TerminalConfigurationChange terminalConfigurationChange => new TerminalConfigurationChangeDto(
                        terminalConfigurationChange.NewRunwayMode.ToDto(),
                        terminalConfigurationChange.LastLandingTimeInPreviousMode,
                        terminalConfigurationChange.FirstLandingTimeInNewMode),
                    LandingRatesChange landingRatesChange => new LandingRatesChangeDto(
                        landingRatesChange.NewLandingRates,
                        landingRatesChange.ChangeTime),
                    null => null,
                    _ => throw new NotImplementedException()
                },
                Slots = _slots
                    .Select(s => s.ToDto())
                    .ToArray(),
                SurfaceWind = new WindDto(SurfaceWind.Direction, SurfaceWind.Speed),
                UpperWind = new WindDto(UpperWind.Direction, UpperWind.Speed),
                ManualWind = ManualWind
            };
        }
    }

    public void Restore(SequenceDto dto)
    {
        _logger.Verbose(
            "Restoring sequence from snapshot ({FlightCount} flights, {SlotCount} slots)",
            dto.Flights.Length, dto.Slots.Length);

        lock (_gate)
        {
            // Clear existing state
            _flights.Clear();
            _slots.Clear();

            CurrentRunwayMode = new RunwayMode(dto.CurrentRunwayMode);

            if (dto.PendingConfigurationChange is not null)
            {
                switch (dto.PendingConfigurationChange)
                {
                    case TerminalConfigurationChangeDto terminalConfigurationChangeDto:
                        PendingConfigurationChange = new TerminalConfigurationChange(
                            new RunwayMode(terminalConfigurationChangeDto.NewRunwayMode),
                            terminalConfigurationChangeDto.LastLandingTimeInPreviousMode,
                            terminalConfigurationChangeDto.FirstLandingTimeInNewMode);
                        break;

                    case LandingRatesChangeDto landingRatesChangeDto:
                        PendingConfigurationChange = new LandingRatesChange(landingRatesChangeDto.NewLandingRates, landingRatesChangeDto.ChangeTime);
                        break;
                }
            }

            // Restore slots
            foreach (var slotDto in dto.Slots)
            {
                var slot = new Slot(slotDto.Id, slotDto.StartTime, slotDto.EndTime, slotDto.RunwayIdentifiers);
                _slots.Add(slot);
            }

            // Restore sequenced flights (both real and manually-inserted)
            foreach (var flightDto in dto.Flights)
            {
                var flight = new Flight(flightDto);
                _flights.Add(flight);
            }

            SurfaceWind = new Wind(dto.SurfaceWind.Direction, dto.SurfaceWind.Speed);
            UpperWind = new Wind(dto.UpperWind.Direction, dto.UpperWind.Speed);
            ManualWind = dto.ManualWind;
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
