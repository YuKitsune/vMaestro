using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Serilog;

namespace Maestro.Core.Model;

// TODO Test cases:
// - Desequencing a flight, deallocates the slot and adds the flight to desequenced flights
// - AddPendingFlight adds a flight to pending flights
// - PurgeSlotsBefore removes slots before a given time and retains the 5 most recent landed flights
// - NumberInSequence returns the index of a flight in the entire sequence, starting from 1
// - NumberForRunway returns the index of a flight for its assigned runway, starting from 1

// TODO: Insert custom slots at specific times
//   - Facilitate manual landing times and no-delay flights
//   - Slot created at a specific time
//   - Duration derived from runway mode if a flight is being inserted
//   - Duration manually specified if a custom slot is being created (i.e. arrival stop)
//   - Conflicting slots removed
//   - Flights in slots that conflict with the custom slot or are behind it are recomputed
//   - If the slot is more than the landing rate behind the prior slot, a reserved slot is created to fill the gap

public class SlotBasedSequence
{
    // TODO: Make these configurable
    static readonly TimeSpan MaxProvisionTime = TimeSpan.FromHours(2);
    static readonly int MaxLandedFlights = 5;

    readonly AirportConfiguration _airportConfiguration;

    readonly List<Flight> _pendingFlights = [];
    readonly List<Flight> _desequencedFlights = [];
    readonly List<Flight> _landedFlights = [];
    readonly List<Slot> _slots = [];

    public string AirportIdentifier => _airportConfiguration.Identifier;
    public string[] FeederFixes => _airportConfiguration.FeederFixes;
    public IReadOnlyList<Flight> PendingFlights => _pendingFlights.AsReadOnly();
    public IReadOnlyList<Flight> DesequencedFlights => _desequencedFlights.AsReadOnly();
    public IReadOnlyList<Flight> LandedFlights => _landedFlights.AsReadOnly();
    public IReadOnlyList<Slot> Slots => _slots.AsReadOnly();
    public IReadOnlyList<Flight> Flights => Slots.Select(s => s.Flight).WhereNotNull().ToList().AsReadOnly();

    public RunwayMode CurrentRunwayMode { get; private set; }
    public RunwayMode? NextRunwayMode { get; private set; }
    public DateTimeOffset RunwayModeChangeTime { get; private set; }

    public SlotBasedSequence(AirportConfiguration airportConfiguration, RunwayMode runwayMode, DateTimeOffset startTime)
    {
        _airportConfiguration = airportConfiguration;
        CurrentRunwayMode = runwayMode;
        ProvisionSlotsFrom(startTime);
    }

    public void ReprovisionSlotsFrom(DateTimeOffset startTime, ISlotBasedScheduler scheduler)
    {
        if (startTime > DateTime.MaxValue.Add(MaxProvisionTime.Negate()))
            throw new MaestroException($"Cannot provision slots for {startTime} as it is too far in the future.");

        var affectedFlights = _slots
            .Where(s => s.Time >= startTime)
            .Select(s => s.Flight)
            .WhereNotNull()
            .ToList();

        var slotsToRemove = _slots.Where(s => s.Time >= startTime).ToArray();
        foreach (var slot in slotsToRemove)
        {
            _slots.Remove(slot);
        }

        ProvisionSlotsFrom(startTime);

        foreach (var affectedFlight in affectedFlights)
        {
            scheduler.AllocateSlot(this, affectedFlight);
        }
    }

    public void ProvisionSlotsFrom(DateTimeOffset startTime)
    {
        if (startTime > DateTime.MaxValue.Add(MaxProvisionTime.Negate()))
            throw new MaestroException($"Cannot provision slots for {startTime} as it is too far in the future.");

        var endTime = startTime + MaxProvisionTime;

        if (NextRunwayMode is not null)
        {
            foreach (var runwayConfiguration in NextRunwayMode.Runways)
            {
                ProvisionSlots(RunwayModeChangeTime, endTime, runwayConfiguration);
            }

            endTime = RunwayModeChangeTime;
        }

        foreach (var runwayConfiguration in CurrentRunwayMode.Runways)
        {
            ProvisionSlots(startTime, endTime, runwayConfiguration);
        }
    }

    void ProvisionSlots(DateTimeOffset startTime, DateTimeOffset endTime, RunwayConfiguration runway)
    {
        var currentTime = startTime;
        while (currentTime < endTime)
        {
            var duration = TimeSpan.FromSeconds(runway.LandingRateSeconds);

            // Ensure no overlapping slots
            if (!_slots.Any(s =>
                    s.RunwayIdentifier == runway.Identifier &&
                    s.Time < currentTime + duration &&
                    s.Time + s.Duration > currentTime))
            {
                _slots.Add(new Slot(runway.Identifier, currentTime, duration));
            }

            currentTime += duration;
        }
    }

    public RunwayMode RunwayModeAt(DateTimeOffset time)
    {
        return NextRunwayMode is not null && RunwayModeChangeTime.IsSameOrBefore(time)
            ? NextRunwayMode
            : CurrentRunwayMode;
    }

    /// <summary>
    ///     Changes the runway mode with an immediate effect.
    /// </summary>
    public void ChangeRunwayMode(RunwayMode runwayMode, ISlotBasedScheduler scheduler, IClock clock)
    {
        CurrentRunwayMode = runwayMode;
        NextRunwayMode = null;
        RunwayModeChangeTime = default;

        ReprovisionSlotsFrom(clock.UtcNow(), scheduler);
    }

    /// <summary>
    ///     Schedules a runway mode change for some time in the future.
    /// </summary>
    public void ChangeRunwayMode(
        RunwayMode runwayMode,
        ISlotBasedScheduler scheduler,
        DateTimeOffset changeTime)
    {
        NextRunwayMode = runwayMode;
        RunwayModeChangeTime = changeTime;

        ReprovisionSlotsFrom(changeTime, scheduler);
    }

    public void DesequenceFlight(string callsign)
    {
        var slot = FindSlotFor(callsign);
        if (slot is null)
            return;

        var flight = slot.Flight!;
        slot.Deallocate();
        _desequencedFlights.Add(flight);
    }

    public void ResumeSequencing(string callsign, ISlotBasedScheduler scheduler)
    {
        var desequencedFlight = _desequencedFlights.SingleOrDefault(f => f.Callsign == callsign);
        if (desequencedFlight is null)
            throw new MaestroException($"{callsign} not found in desequenced list");

        _desequencedFlights.Remove(desequencedFlight);
        scheduler.AllocateSlot(this, desequencedFlight);
    }

    public void AddPendingFlight(Flight flight)
    {
        if (_pendingFlights.Any(f => f.Callsign == flight.Callsign))
            throw new MaestroException($"{flight.Callsign} is already pending");

        _pendingFlights.Add(flight);
    }

    public void PurgeEmptySlotsBefore(DateTimeOffset dateTimeOffset)
    {
        var flights = new List<Flight>();
        var slotsToRemove = _slots.Where(s => s.IsAvailable && s.Time.IsSameOrBefore(dateTimeOffset)).ToArray();
        foreach (var slot in slotsToRemove)
        {
            if (slot.Flight is not null)
            {
                flights.Add(slot.Flight);
            }

            _slots.Remove(slot);
        }

        _landedFlights.AddRange(flights);
        if (_landedFlights.Count > MaxLandedFlights)
        {
            var excess = _landedFlights.Count - MaxLandedFlights;
            _landedFlights.RemoveRange(0, excess);
        }
    }

    public void PurgeLandedFlights()
    {
        var slotsWithLandedFlights = _slots
            .Where(s => s.Flight is not null && s.Flight.State == State.Landed)
            .ToArray();

        foreach (var slot in slotsWithLandedFlights)
        {
            _slots.Remove(slot);
        }

        var landedFlights = slotsWithLandedFlights
            .Select(s => s.Flight!);

        _landedFlights.AddRange(landedFlights);
        if (_landedFlights.Count > MaxLandedFlights)
        {
            var excess = _landedFlights.Count - MaxLandedFlights;
            _landedFlights.RemoveRange(0, excess);
        }
    }

    public int NumberInSequence(Flight flight)
    {
        return _slots
            .Select(s => s.Flight)
            .WhereNotNull()
            .ToList()
            .IndexOf(flight) + 1;
    }

    public int NumberForRunway(Flight flight)
    {
        return _slots
            .Where(s => s.RunwayIdentifier == flight.AssignedRunwayIdentifier)
            .Select(s => s.Flight)
            .WhereNotNull()
            .ToList()
            .IndexOf(flight) + 1;
    }

    public void Clear()
    {
        _slots.Clear();
        _desequencedFlights.Clear();
        _pendingFlights.Clear();
        NextRunwayMode = null;
        RunwayModeChangeTime = default;
    }

    public void RecordGaps(ILogger logger)
    {
        if (_slots.Count < 2)
            return;

        // Sort the slots by their start time
        var orderedSlots = _slots.OrderBy(s => s.Time).ToList();

        for (int i = 0; i < orderedSlots.Count - 1; i++)
        {
            var currentSlot = orderedSlots[i];
            var nextSlot = orderedSlots[i + 1];

            if (currentSlot.Time + currentSlot.Duration < nextSlot.Time)
            {
                var gapStart = currentSlot.Time + currentSlot.Duration;
                var gapEnd = nextSlot.Time;
                logger.Information($"Gap detected between slots: {gapStart} to {gapEnd}");
            }
        }
    }

    public Slot? FindSlotFor(string callsign)
    {
        return _slots.SingleOrDefault(s => s.Flight?.Callsign == callsign);
    }

    public Flight? FindFlight(string callsign)
    {
        return _slots
            .Select(s => s.Flight)
            .WhereNotNull()
            .SingleOrDefault(f => f.Callsign == callsign);
    }
}
