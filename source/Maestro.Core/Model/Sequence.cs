using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

// TODO Test cases:
// - Desequencing a flight, deallocates the slot and adds the flight to desequenced flights
// - AddPendingFlight adds a flight to pending flights
// - PurgeSlotsBefore removes slots before a given time and retains the 5 most recent landed flights
// - NumberInSequence returns the index of a flight in the entire sequence, starting from 1
// - NumberForRunway returns the index of a flight for its assigned runway, starting from 1

public class Sequence
{
    private int _dummyCounter = 1;

    readonly AirportConfiguration _airportConfiguration;

    readonly List<Flight> _trackedFlights = [];
    readonly List<Slot> _slots = [];

    public string AirportIdentifier => _airportConfiguration.Identifier;
    public string[] FeederFixes => _airportConfiguration.FeederFixes;
    public IReadOnlyList<Flight> Flights => _trackedFlights.AsReadOnly();

    public RunwayMode CurrentRunwayMode { get; private set; }
    public DateTimeOffset LastLandingTimeForCurrentMode { get; private set; }
    public RunwayMode? NextRunwayMode { get; private set; }
    public DateTimeOffset FirstLandingTimeForNextMode { get; private set; }

    public IReadOnlyList<Slot> Slots => _slots.AsReadOnly();

    public Sequence(AirportConfiguration airportConfiguration, RunwayMode runwayMode)
    {
        _airportConfiguration = airportConfiguration;
        CurrentRunwayMode = runwayMode;
    }

    public void TrySwapRunwayModes(IClock clock)
    {
        if (NextRunwayMode is null)
            return;

        if (FirstLandingTimeForNextMode.IsAfter(clock.UtcNow()))
            return;

        CurrentRunwayMode = NextRunwayMode;
        NextRunwayMode = null;
        LastLandingTimeForCurrentMode = default;
        FirstLandingTimeForNextMode = default;
    }

    public RunwayMode GetRunwayModeAt(DateTimeOffset time)
    {
        if (NextRunwayMode is null)
            return CurrentRunwayMode;

        if (time.IsSameOrBefore(LastLandingTimeForCurrentMode))
            return CurrentRunwayMode;

        return NextRunwayMode;
    }

    /// <summary>
    ///     Changes the runway mode with an immediate effect.
    /// </summary>
    public void ChangeRunwayMode(RunwayMode runwayMode, IScheduler scheduler)
    {
        CurrentRunwayMode = runwayMode;
        NextRunwayMode = null;
        LastLandingTimeForCurrentMode = default;
        FirstLandingTimeForNextMode = default;
        scheduler.Schedule(this);
    }

    /// <summary>
    ///     Schedules a runway mode change for some time in the future.
    /// </summary>
    public void ChangeRunwayMode(
        RunwayMode runwayMode,
        DateTimeOffset lastLandingTimeForOldMode,
        DateTimeOffset firstLandingTimeForNewMode,
        IScheduler scheduler)
    {
        NextRunwayMode = runwayMode;
        LastLandingTimeForCurrentMode = lastLandingTimeForOldMode;
        FirstLandingTimeForNextMode = firstLandingTimeForNewMode;
        scheduler.Schedule(this, recalculateAll: true);
    }

    public void AddDummyFlight(DateTimeOffset landingTime, string runwayIdentifier, IScheduler scheduler, IClock clock)
    {
        var callsign = $"****{_dummyCounter++:00}*";

        var flight = new Flight(callsign, AirportIdentifier, landingTime)
        {
            IsDummy = true
        };

        flight.SetRunway(runwayIdentifier, manual: true);
        flight.SetLandingTime(landingTime, manual: true);

        _trackedFlights.Add(flight);
        scheduler.Schedule(this);
        flight.SetState(State.Frozen, clock);
    }

    public void AddFlight(Flight flight, IScheduler scheduler)
    {
        if (_trackedFlights.Any(f => f.Callsign == flight.Callsign))
            throw new MaestroException($"{flight.Callsign} is already tracked");

        // TODO: Validate state

        _trackedFlights.Add(flight);
        scheduler.Schedule(this);
    }

    public Flight? FindTrackedFlight(string callsign)
    {
        return _trackedFlights.FirstOrDefault(f => f.Callsign == callsign);
    }

    public void DesequenceFlight(string callsign, IScheduler scheduler)
    {
        var flight = FindTrackedFlight(callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found");

        flight.Desequence();
        scheduler.Schedule(this);
    }

    public void ResumeSequencing(string callsign, IScheduler scheduler)
    {
        var flight = FindTrackedFlight(callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found");

        flight.Resume();
        scheduler.Schedule(this);
    }

    public void RemoveFlight(string callsign, IScheduler scheduler)
    {
        var flight = FindTrackedFlight(callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found in desequenced list");

        flight.Remove();
        scheduler.Schedule(this);
    }

    public void Delete(Flight flight)
    {
        _trackedFlights.Remove(flight);
    }

    public Slot CreateSlot(DateTimeOffset start, DateTimeOffset end, string[] runwayIdentifiers, IScheduler scheduler)
    {
        var slot = new Slot(Guid.NewGuid(), start, end, runwayIdentifiers);
        _slots.Add(slot);
        scheduler.Schedule(this, recalculateAll: true);
        return slot;
    }

    public Slot ModifySlot(Guid id, DateTimeOffset start, DateTimeOffset end, IScheduler scheduler)
    {
        var slot = _slots.FirstOrDefault(s => s.Id == id);
        if (slot is null)
            throw new MaestroException("Slot not found");

        slot.ChangeTime(start, end);
        scheduler.Schedule(this);

        return slot;
    }

    public void DeleteSlot(Guid id, IScheduler scheduler)
    {
        var slot = _slots.FirstOrDefault(s => s.Id == id);
        if (slot is null)
            throw new MaestroException("Slot not found");

        _slots.Remove(slot);

        scheduler.Schedule(this);
    }

    public int NumberInSequence(Flight flight)
    {
        return _trackedFlights
            .Where(f => f.Activated)
            .ToList()
            .IndexOf(flight) + 1;
    }

    public int NumberForRunway(Flight flight)
    {
        return _trackedFlights
            .Where(f => f.Activated)
            .Where(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier)
            .ToList()
            .IndexOf(flight) + 1;
    }
}
