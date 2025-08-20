﻿using Maestro.Core.Configuration;
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

public class Sequence
{
    readonly AirportConfiguration _airportConfiguration;

    readonly List<Flight> _pendingFlights = [];
    readonly List<Flight> _trackedFlights = [];

    public string AirportIdentifier => _airportConfiguration.Identifier;
    public string[] FeederFixes => _airportConfiguration.FeederFixes;
    public IReadOnlyList<Flight> PendingFlights => _pendingFlights.AsReadOnly();
    public IReadOnlyList<Flight> Flights => _trackedFlights.AsReadOnly();

    public RunwayMode CurrentRunwayMode { get; private set; }
    public DateTimeOffset LastLandingTimeForCurrentMode { get; private set; }
    public RunwayMode? NextRunwayMode { get; private set; }
    public DateTimeOffset FirstLandingTimeForNextMode { get; private set; }

    public Sequence(AirportConfiguration airportConfiguration, RunwayMode runwayMode)
    {
        _airportConfiguration = airportConfiguration;
        CurrentRunwayMode = runwayMode;
    }

    public RunwayMode RunwayModeAt(DateTimeOffset time)
    {
        return NextRunwayMode is not null && FirstLandingTimeForNextMode.IsSameOrBefore(time)
            ? NextRunwayMode
            : CurrentRunwayMode;
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
        CreateSlotInternal(lastLandingTimeForOldMode, firstLandingTimeForNewMode);

        NextRunwayMode = runwayMode;
        LastLandingTimeForCurrentMode = lastLandingTimeForOldMode;
        FirstLandingTimeForNextMode = firstLandingTimeForNewMode;
        scheduler.Schedule(this);
    }

    /// <summary>
    ///     Creates a slot for the given time range in which no flights can land.
    /// </summary>
    public void CreateSlot(DateTimeOffset startTime, DateTimeOffset endTime, IScheduler scheduler)
    {
        CreateSlotInternal(startTime, endTime);
        scheduler.Schedule(this);
    }

    void CreateSlotInternal(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        if (startTime >= endTime)
            throw new MaestroException("Start time must be before end time");

        throw new NotImplementedException();
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

    public void AddPendingFlight(Flight flight)
    {
        if (_pendingFlights.Any(f => f.Callsign == flight.Callsign))
            throw new MaestroException($"{flight.Callsign} is already pending");

        _pendingFlights.Add(flight);
    }

    public int NumberInSequence(Flight flight)
    {
        return _trackedFlights
            .Where(f => f.IsInSequence)
            .ToList()
            .IndexOf(flight) + 1;
    }

    public int NumberForRunway(Flight flight)
    {
        return _trackedFlights
            .Where(f => f.IsInSequence)
            .Where(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier)
            .ToList()
            .IndexOf(flight) + 1;
    }

    public void Clear()
    {
        _trackedFlights.Clear();
        _pendingFlights.Clear();
        NextRunwayMode = null;
        LastLandingTimeForCurrentMode = default;
        FirstLandingTimeForNextMode = default;
    }
}
