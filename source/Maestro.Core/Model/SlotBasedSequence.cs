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

// TODO: New idea
//   - Create a slot for every minute
//   - Mark slots that are too close together (based on landing rate) as unavailable
//   - Ensure slots are rounded to the minute.
//   - Allow flights to be moved to unavailable slots if placed there manually, or NoDelay is applied
//     - Mark following slots as unavailable as per the landing rate

// or:
//   - Stick to the current implementation, but if the preceedng slot is free, shift the flight forward by 1 slot max
//   - Stick to the current implementation, but if the preceedng slot is free, delete it and shift the following slot forward so there is no delay

public class SlotBasedSequence
{
    // TODO: Make this configurable
    static readonly int MaxLandedFlights = 5;

    readonly AirportConfiguration _airportConfiguration;

    readonly List<Flight> _pendingFlights = [];
    readonly List<Flight> _desequencedFlights = [];
    readonly List<Flight> _landedFlights = [];
    readonly List<Flight> _sequencedFlights = [];

    public string AirportIdentifier => _airportConfiguration.Identifier;
    public string[] FeederFixes => _airportConfiguration.FeederFixes;
    public IReadOnlyList<Flight> PendingFlights => _pendingFlights.AsReadOnly();
    public IReadOnlyList<Flight> DesequencedFlights => _desequencedFlights.AsReadOnly();
    public IReadOnlyList<Flight> LandedFlights => _landedFlights.AsReadOnly();
    public IReadOnlyList<Flight> Flights => _sequencedFlights.AsReadOnly();

    public RunwayMode CurrentRunwayMode { get; private set; }
    public RunwayMode? NextRunwayMode { get; private set; }
    public DateTimeOffset RunwayModeChangeTime { get; private set; }

    public SlotBasedSequence(AirportConfiguration airportConfiguration, RunwayMode runwayMode)
    {
        _airportConfiguration = airportConfiguration;
        CurrentRunwayMode = runwayMode;
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
    public void ChangeRunwayMode(RunwayMode runwayMode, ISlotBasedScheduler scheduler)
    {
        CurrentRunwayMode = runwayMode;
        NextRunwayMode = null;
        RunwayModeChangeTime = default;
        scheduler.Schedule(this);
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
        scheduler.Schedule(this);
    }

    public void DesequenceFlight(string callsign, ISlotBasedScheduler scheduler)
    {
        var flight = _sequencedFlights.SingleOrDefault(f => f.Callsign == callsign);
        if (flight is null)
            throw new MaestroException($"{callsign} not found");

        _sequencedFlights.Remove(flight);
        _desequencedFlights.Add(flight);
        scheduler.Schedule(this);
    }

    public void ResumeSequencing(string callsign, ISlotBasedScheduler scheduler)
    {
        var desequencedFlight = _desequencedFlights.SingleOrDefault(f => f.Callsign == callsign);
        if (desequencedFlight is null)
            throw new MaestroException($"{callsign} not found in desequenced list");

        _desequencedFlights.Remove(desequencedFlight);
        _sequencedFlights.Add(desequencedFlight);
        scheduler.Schedule(this);
    }

    public void AddPendingFlight(Flight flight)
    {
        if (_pendingFlights.Any(f => f.Callsign == flight.Callsign))
            throw new MaestroException($"{flight.Callsign} is already pending");

        _pendingFlights.Add(flight);
    }

    public void MarkAsLanded(Flight flight)
    {
        if (_landedFlights.Any(f => f.Callsign == flight.Callsign))
            throw new MaestroException($"{flight.Callsign} is already marked as landed");

        _landedFlights.Add(flight);
        _sequencedFlights.Remove(flight);

        // Keep only the most recent 5 landed flights
        if (_landedFlights.Count > MaxLandedFlights)
        {
            _landedFlights.RemoveAt(0);
        }
    }

    public int NumberInSequence(Flight flight)
    {
        return _sequencedFlights
            .ToList()
            .IndexOf(flight) + 1;
    }

    public int NumberForRunway(Flight flight)
    {
        return _sequencedFlights
            .Where(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier)
            .ToList()
            .IndexOf(flight) + 1;
    }

    public void Clear()
    {
        _sequencedFlights.Clear();
        _desequencedFlights.Clear();
        _pendingFlights.Clear();
        NextRunwayMode = null;
        RunwayModeChangeTime = default;
    }
}
