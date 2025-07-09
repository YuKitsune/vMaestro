using System.Data;
using System.Diagnostics;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;

namespace Maestro.Core.Model;

[DebuggerDisplay("{AirportIdentifier}")]
public class Sequence
{
    readonly AirportConfiguration _airportConfiguration;

    readonly List<BlockoutPeriod> _blockoutPeriods = new();
    readonly List<Flight> _pending = new();
    readonly List<Flight> _flights = new();

    public string AirportIdentifier => _airportConfiguration.Identifier;
    public string[] FeederFixes => _airportConfiguration.FeederFixes;

    public IReadOnlyList<BlockoutPeriod> BlockoutPeriods => _blockoutPeriods;
    public Flight[] Flights => _flights.ToArray();
    public Flight[] SequencableFlights => _flights.Where(f => f.ShouldSequence).ToArray();
    public RunwayMode CurrentRunwayMode { get; private set; }
    public RunwayMode? NextRunwayMode { get; private set; }
    public DateTimeOffset RunwayModeChangeTime { get; private set; }
    public IReadOnlyList<RunwayAssignmentRule> RunwayAssignmentRules => _airportConfiguration.RunwayAssignmentRules;

    public Sequence(AirportConfiguration airportConfiguration)
    {
        _airportConfiguration = airportConfiguration;
        CurrentRunwayMode = airportConfiguration.RunwayModes.First();
    }

    public void Add(Flight flight)
    {
        if (_flights.Any(f => f.Callsign == flight.Callsign))
            throw new MaestroException($"{flight.Callsign} is already in the sequence for {AirportIdentifier}.");

        if (flight.DestinationIdentifier != AirportIdentifier)
            throw new MaestroException($"{flight.Callsign} cannot be added to the sequence for {AirportIdentifier} as the destination is {flight.DestinationIdentifier}.");

        if (!flight.Activated)
            throw new MaestroException($"{flight.Callsign} cannot be sequenced as it is not activated.");

        // TODO: Additional validation

        _flights.Add(flight);
        _flights.Sort(FlightComparer.Instance);
    }

    public void Schedule(IScheduler scheduler)
    {
        _flights.Sort(FlightComparer.Instance);

        foreach (var flight in SequencableFlights)
        {
            scheduler.Schedule(this, flight);
        }

        _flights.Sort(FlightComparer.Instance);
    }

    public void AddPending(Flight flight)
    {
        if (_pending.Any(f => f.Callsign == flight.Callsign))
            throw new MaestroException($"{flight.Callsign} is already in the Pending list for {AirportIdentifier}.");

        if (flight.DestinationIdentifier != AirportIdentifier)
            throw new MaestroException($"{flight.Callsign} cannot be added to the Pending list for {AirportIdentifier} as the destination is {flight.DestinationIdentifier}.");

        // TODO: Additional validation
        _pending.Add(flight);
    }

    public void AddBlockout(BlockoutPeriod blockoutPeriod)
    {
        // TODO: Prevent overlaps
        _blockoutPeriods.Add(blockoutPeriod);
    }

    public Flight? TryGetFlight(string callsign)
    {
        return _flights.FirstOrDefault(f => f.Callsign == callsign);
    }

    /// <summary>
    ///     Changes the runway mode with an immediate effect.
    /// </summary>
    public void ChangeRunwayMode(RunwayMode runwayMode)
    {
        CurrentRunwayMode = runwayMode;
        NextRunwayMode = null;
        RunwayModeChangeTime = default;
    }

    /// <summary>
    ///     Schedules a runway mode change for some time in the future.
    /// </summary>
    public void ChangeRunwayMode(
        RunwayMode runwayMode,
        DateTimeOffset changeTime)
    {
        NextRunwayMode = runwayMode;
        RunwayModeChangeTime = changeTime;
    }

    public RunwayMode GetRunwayModeAt(DateTimeOffset targetTime)
    {
        if (NextRunwayMode is not null && RunwayModeChangeTime.IsSameOrBefore(targetTime))
        {
            return NextRunwayMode;
        }

        return CurrentRunwayMode;
    }

    public int NumberInSequence(Flight flight)
    {
        return _flights.IndexOf(flight) + 1;
    }

    public int NumberForRunway(Flight flight)
    {
        return _flights.Where(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier)
            .ToList()
            .IndexOf(flight) + 1;
    }

    /// <summary>
    ///     Completely un-tracks the provided <paramref name="flight"/>.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if the flight was deleted. <c>false</c> otherwise.
    /// </returns>
    /// <remarks>
    ///     If an FDR update is published for this flight, it can be tracked again.
    ///     To remove a flight from the sequence and prevent it from being tracked, use
    ///     <see cref="Flight"/>.<see cref="Flight.Remove()"/>.
    /// </remarks>
    public bool Delete(Flight flight)
    {
        var result = _flights.Remove(flight);
        _flights.Sort(FlightComparer.Instance);

        return result;
    }

    public void Clear()
    {
        _blockoutPeriods.Clear();
        _flights.Clear();
        _pending.Clear();
        NextRunwayMode = null;
        RunwayModeChangeTime = default;
    }

    // public Flight[] ComputeSequence_OLD(IReadOnlyList<Flight> flights)
    // {
    //     var sequence = new List<Flight>();
    //     // var maxDelayPass = new List<Flight>();
    //     var frozenPass = new List<Flight>();
    //
    //     foreach (var flight in flights)
    //     {
    //         switch (flight.State)
    //         {
    //             case State.Frozen:
    //             case State.SuperStable:
    //                 frozenPass.Add(flight);
    //                 break;
    //
    //             case State.Stable:
    //             case State.Unstable:
    //                 if (sequence.Count == 0)
    //                 {
    //                     BlockoutPeriod? blockoutPeriod = null;
    //                     if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
    //                         blockoutPeriod = FindBlockoutPeriodAt(
    //                             flight.EstimatedLandingTime,
    //                             flight.AssignedRunwayIdentifier);
    //
    //                     if (blockoutPeriod is null)
    //                     {
    //                         flight.SetFlowControls(FlowControls.ProfileSpeed);
    //
    //                         CalculateEstimates(flight);
    //
    //                         if (flight.EstimatedFeederFixTime.HasValue)
    //                             flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
    //
    //                         flight.SetLandingTime(flight.EstimatedLandingTime);
    //                     }
    //                     else
    //                     {
    //                         // Set the landing time to the end of the blockout period
    //                         if (flight.EstimatedFeederFixTime.HasValue)
    //                         {
    //                             // TODO: Calculate STAR ETI
    //                             var feederFixTime = blockoutPeriod.EndTime -
    //                                                 (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value);
    //                             flight.SetFeederFixTime(feederFixTime);
    //                         }
    //
    //                         flight.SetLandingTime(blockoutPeriod.EndTime);
    //                     }
    //                 }
    //                 else
    //                 {
    //                     var leadingFlight = sequence.Last();
    //                     var landingTime = leadingFlight.ScheduledLandingTime + _separationRuleProvider.GetRequiredSpacing(leadingFlight, flight, CurrentRunwayMode);
    //
    //                     BlockoutPeriod? blockoutPeriod = null;
    //                     if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
    //                         blockoutPeriod = FindBlockoutPeriodAt(
    //                             flight.EstimatedLandingTime,
    //                             flight.AssignedRunwayIdentifier);
    //
    //                     // Inside blockout period, push back landing time
    //                     var earliestLandingTime = blockoutPeriod?.EndTime ?? landingTime;
    //                     if (blockoutPeriod is not null)
    //                     {
    //                         // TODO: Re-calculate runway assignment when the runway mode is going to change
    //                         AssignFeeder(flight);
    //                         AssignRunway(flight);
    //                         CalculateEstimates(flight);
    //                     }
    //
    //                     // Don't speed the flight up to make the earliest landing time
    //                     if (earliestLandingTime < flight.EstimatedLandingTime)
    //                     {
    //                         if (flight.EstimatedFeederFixTime.HasValue)
    //                             flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
    //
    //                         flight.SetLandingTime(flight.EstimatedLandingTime);
    //                         flight.SetFlowControls(FlowControls.ProfileSpeed);
    //                         CalculateEstimates(flight);
    //                         break;
    //                     }
    //
    //                     // if (flight.MaxDelay < TimeSpan.MaxValue && flight.EstimatedLandingTime != earliestLandingTime)//insert these after first pass
    //                     // {
    //                     //     maxDelayPass.Add(flight);
    //                     //     break;
    //                     // }
    //
    //                     // If we're here, there is a delay
    //                     var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
    //                     if (performanceData is not null && performanceData.IsJet)
    //                     {
    //                         flight.SetFlowControls(FlowControls.S250);
    //                         CalculateEstimates(flight);
    //                     }
    //
    //                     // Get STA_FF from STAR ETI
    //                     if (flight.FeederFixIdentifier is not null &&
    //                         flight.AssignedStarIdentifier is not null &&
    //                         flight.AssignedRunwayIdentifier is not null)
    //                     {
    //                         var arrivalInterval = _arrivalLookup.GetArrivalInterval(
    //                             flight.DestinationIdentifier,
    //                             flight.AssignedStarIdentifier,
    //                             flight.AssignedRunwayIdentifier);
    //
    //                         if (arrivalInterval is not null)
    //                         {
    //                             var feederFixTime = landingTime - arrivalInterval.Value;
    //                             flight.SetFeederFixTime(feederFixTime);
    //                         }
    //                     }
    //                     else
    //                     {
    //                         if (flight.EstimatedFeederFixTime.HasValue)
    //                             flight.SetFeederFixTime(landingTime - (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value));
    //                     }
    //
    //                     flight.SetLandingTime(landingTime);
    //                 }
    //
    //                 sequence.Add(flight);
    //                 break;
    //         }
    //     }
    //
    //     // foreach (var flight in maxDelayPass.OrderBy(f => f.EstimatedLandingTime).ThenBy(f => f.TotalDelayToRunway))
    //     // {
    //     //     var i = sequence.FindLastIndex(f =>
    //     //         flight.ScheduledLandingTime + GetRequiredSpacing(f, flight) - flight.EstimatedLandingTime < flight.MaxDelay + GetRequiredSpacing(f, flight)) + 1;
    //     //     sequence.Insert(i, flight);
    //     //
    //     //     flight.SetLandingTime(flight.EstimatedLandingTime);
    //     //     if (flight.EstimatedFeederFixTime.HasValue)
    //     //         flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
    //     //
    //     //     DoSequencePass(i, sequence);
    //     // }
    //
    //     // TODO: Confirm what this does
    //     foreach (var flight in frozenPass.OrderBy(f => f.ScheduledLandingTime))
    //     {
    //         var orderedTimes = flights.OrderBy(k => k.ScheduledLandingTime).ToList();
    //
    //         // var i = sequence.FindLastIndex(f => (f.State > Flight.States.Stable && sequenceTimes[f][0] < flight.STA) || (sequenceTimes[f][0] + GetRequiredSpacing(airport, f, flight) < flight.STA)) + 1;
    //         var leader = orderedTimes.FindLast(k =>
    //             (k.State > State.Stable && k.ScheduledLandingTime < flight.ScheduledLandingTime) ||
    //             (k.ScheduledLandingTime + _separationRuleProvider.GetRequiredSpacing(k, flight, CurrentRunwayMode) < flight.ScheduledLandingTime));
    //
    //         var i = 0;
    //         if (leader is not null)
    //             i = sequence.IndexOf(leader) + 1;
    //
    //         sequence.Insert(i, flight);
    //         // sequenceTimes.Add(flight, new DateTime[2] { flight.STA, flight.STA_FF });
    //
    //         DoSequencePass(i, sequence);
    //     }
    //
    //     return sequence.ToArray();
    // }
    //
    // void DoSequencePass(int position, List<Flight> sequence)
    // {
    //     for (var i = position; i < sequence.Count; i++)
    //     {
    //         var flight = sequence[i];
    //
    //         // Do not alter STA for anything SuperStable or beyond
    //         if (flight.State > State.Stable)
    //             continue;
    //
    //         if (i == 0)
    //         {
    //             var landingTime = flight.EstimatedLandingTime;
    //
    //             BlockoutPeriod? blockoutPeriod = null;
    //             if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
    //                 blockoutPeriod = FindBlockoutPeriodAt(
    //                     flight.EstimatedLandingTime,
    //                     flight.AssignedRunwayIdentifier);
    //
    //             if (blockoutPeriod is not null)
    //             {
    //                 // TODO:
    //                 // if (f.ModeProcessed != airport.FutureMode)
    //                 // {
    //                 //     AssignFeeder(airport, f);
    //                 //     AssignRunway(airport, f);
    //                 //     CalculateEstimates(airport, f);
    //                 // }
    //
    //                 AssignFeeder(flight);
    //                 AssignRunway(flight);
    //                 CalculateEstimates(flight);
    //
    //                 landingTime = blockoutPeriod.EndTime;
    //             }
    //
    //             if (landingTime == flight.EstimatedLandingTime)
    //             {
    //                 flight.SetLandingTime(landingTime);
    //
    //                 if (flight.EstimatedFeederFixTime.HasValue)
    //                     flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
    //             }
    //             else
    //             {
    //                 var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
    //                 if (performanceData is not null && performanceData.IsJet)
    //                 {
    //                     flight.SetFlowControls(FlowControls.S250);
    //                     CalculateEstimates(flight);
    //                 }
    //
    //                 // Get STA_FF from STAR ETI
    //                 if (flight.FeederFixIdentifier is not null &&
    //                     flight.AssignedStarIdentifier is not null &&
    //                     flight.AssignedRunwayIdentifier is not null)
    //                 {
    //                     var arrivalInterval = _arrivalLookup.GetArrivalInterval(
    //                         flight.DestinationIdentifier,
    //                         flight.AssignedStarIdentifier,
    //                         flight.AssignedRunwayIdentifier);
    //
    //                     if (arrivalInterval is not null)
    //                     {
    //                         var feederFixTime = landingTime - arrivalInterval.Value;
    //                         flight.SetFeederFixTime(feederFixTime);
    //                     }
    //                 }
    //                 else
    //                 {
    //                     if (flight.EstimatedFeederFixTime.HasValue)
    //                         flight.SetFeederFixTime(landingTime - (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value));
    //                 }
    //
    //                 flight.SetLandingTime(landingTime);
    //             }
    //         }
    //         else
    //         {
    //             var leader = sequence[i - 1];
    //
    //             var landingTime = leader.EstimatedLandingTime + _separationRuleProvider.GetRequiredSpacing(leader, flight, CurrentRunwayMode);
    //
    //             BlockoutPeriod? blockoutPeriod = null;
    //             if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
    //                 blockoutPeriod = FindBlockoutPeriodAt(
    //                     flight.EstimatedLandingTime,
    //                     flight.AssignedRunwayIdentifier);
    //
    //             if (blockoutPeriod is not null)
    //             {
    //                 // TODO:
    //                 // if (f.ModeProcessed != airport.FutureMode)
    //                 // {
    //                 //     AssignFeeder(airport, f);
    //                 //     AssignRunway(airport, f);
    //                 //     CalculateEstimates(airport, f);
    //                 // }
    //
    //                 AssignFeeder(flight);
    //                 AssignRunway(flight);
    //                 CalculateEstimates(flight);
    //
    //                 landingTime = blockoutPeriod.EndTime;
    //             }
    //
    //             if (landingTime <= flight.EstimatedLandingTime)
    //             {
    //                 flight.SetLandingTime(landingTime);
    //
    //                 if (flight.EstimatedFeederFixTime.HasValue)
    //                     flight.SetFeederFixTime(landingTime + (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value));
    //
    //                 // There are no more changes to process if this time is just ETA.
    //                 if (i != 1)
    //                     break;
    //             }
    //             else
    //             {
    //                 // If we're here, there is a delay
    //                 var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
    //                 if (performanceData is not null && performanceData.IsJet)
    //                 {
    //                     flight.SetFlowControls(FlowControls.S250);
    //                     CalculateEstimates(flight);
    //                 }
    //
    //                 // Get STA_FF from STAR ETI
    //                 if (flight.FeederFixIdentifier is not null &&
    //                     flight.AssignedStarIdentifier is not null &&
    //                     flight.AssignedRunwayIdentifier is not null)
    //                 {
    //                     var arrivalInterval = _arrivalLookup.GetArrivalInterval(
    //                         flight.DestinationIdentifier,
    //                         flight.AssignedStarIdentifier,
    //                         flight.AssignedRunwayIdentifier);
    //
    //                     if (arrivalInterval is not null)
    //                     {
    //                         var feederFixTime = landingTime - arrivalInterval.Value;
    //                         flight.SetFeederFixTime(feederFixTime);
    //                     }
    //                 }
    //                 else
    //                 {
    //                     if (flight.EstimatedFeederFixTime.HasValue)
    //                         flight.SetFeederFixTime(landingTime - (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value));
    //                 }
    //
    //                 flight.SetLandingTime(landingTime);
    //             }
    //         }
    //     }
    // }
}
