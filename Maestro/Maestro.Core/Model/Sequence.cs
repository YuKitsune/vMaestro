using System.Diagnostics;
using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Core.Model;

[DebuggerDisplay("{AirportIdentifier}")]
public class Sequence : IAsyncDisposable
{
    readonly IMediator _mediator;
    readonly AirportConfiguration _airportConfiguration;
    readonly IScheduler _scheduler;
    readonly IEstimateProvider _estimateProvider;
    readonly IClock _clock;
    
    readonly List<BlockoutPeriod> _blockoutPeriods = new();
    readonly List<Flight> _pending = new();
    readonly List<Flight> _flights = new();
    
    readonly SemaphoreSlim _semaphore = new(1, 1);
    
    CancellationTokenSource? _sequenceTaskCancellationSource;
    Task? _sequenceTask;
    
    public string AirportIdentifier => _airportConfiguration.Identifier;
    public string[] FeederFixes => _airportConfiguration.FeederFixes;

    public IReadOnlyList<Flight> Flights => _flights.AsReadOnly();

    public RunwayModeConfiguration CurrentRunwayMode { get; }

    public Sequence(
        AirportConfiguration airportConfiguration,
        IMediator mediator,
        IClock clock,
        IEstimateProvider estimateProvider, IScheduler scheduler)
    {
        _airportConfiguration = airportConfiguration;
        _mediator = mediator;
        _clock = clock;
        _estimateProvider = estimateProvider;
        _scheduler = scheduler;

        CurrentRunwayMode = airportConfiguration.RunwayModes.First();
    }

    public void Start()
    {
        if (_sequenceTask != null)
            throw new MaestroException($"Sequence for {AirportIdentifier} is already running");

        _sequenceTaskCancellationSource = new CancellationTokenSource();
        _sequenceTask = DoSequence(_sequenceTaskCancellationSource.Token);
    }

    public async Task Stop()
    {
        if (_sequenceTask is null || _sequenceTaskCancellationSource is null)
            throw new MaestroException($"Sequence for {AirportIdentifier} has not been started");
        
        _sequenceTaskCancellationSource.Cancel();
        await _sequenceTask;
        _sequenceTask = null;
        _sequenceTaskCancellationSource = null;
    }

    public async Task Add(Flight flight, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_flights.Any(f => f.Callsign == flight.Callsign))
                throw new MaestroException($"{flight.Callsign} is already in the sequence for {AirportIdentifier}.");
            
            if (flight.DestinationIdentifier != AirportIdentifier)
                throw new MaestroException($"{flight.Callsign} cannot be added to the sequence for {AirportIdentifier} as the destination is {flight.DestinationIdentifier}.");
            
            if (!flight.Activated)
                throw new MaestroException($"{flight.Callsign} cannot be sequenced as it is not activated.");
            
            // TODO: Additional validation

            AssignFeeder(flight);
            CalculateEstimates(flight);
            AssignRunway(flight);
            
            // Add the flight based on it's estimate
            if (_flights.Count == 0)
            {
                _flights.Add(flight);
                return;
            }
            
            // TODO: Account for runway mode changes if necessary
            var index = _flights.FindLastIndex(f => !f.PositionIsFixed && f.EstimatedLandingTime < flight.EstimatedLandingTime) + 1;
            _flights.Insert(index, flight);
            
            await _mediator.Publish(new SequenceModifiedNotification(this), cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RepositionByEstimate(Flight flight, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_flights.Count == 0)
            {
                _flights.Add(flight);
                return;
            }

            // TODO: Account for runway mode changes if necessary
            var index = _flights.FindLastIndex(f =>
                !f.PositionIsFixed && f.EstimatedLandingTime < flight.EstimatedLandingTime) + 1;

            var currentIndex = _flights.IndexOf(flight);
            if (currentIndex == -1)
            {
                _flights.Insert(index, flight);
            }
            else if (currentIndex != index)
            {
                if (index > currentIndex) index--;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddPending(Flight flight, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_pending.Any(f => f.Callsign == flight.Callsign))
                throw new MaestroException($"{flight.Callsign} is already in the Pending list for {AirportIdentifier}.");
            
            if (flight.DestinationIdentifier != AirportIdentifier)
                throw new MaestroException($"{flight.Callsign} cannot be added to the Pending list for {AirportIdentifier} as the destination is {flight.DestinationIdentifier}.");
            
            // TODO: Additional validation
            _pending.Add(flight);
            
            await _mediator.Publish(new SequenceModifiedNotification(this), cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Flight?> TryGetFlight(string callsign, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return _flights.FirstOrDefault(f => f.Callsign == callsign);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    async Task DoSequence(CancellationToken cancellationToken)
    {
        // TODO: Make configurable
        var calculationIntervalSeconds = 10;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // TODO: Remove completed flights after a certain time

                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    foreach (var flight in _flights)
                    {
                        CalculateEstimates(flight);
                    }
                    
                    var flights = _scheduler.ScheduleFlights(
                        _flights,
                        _blockoutPeriods.ToArray(),
                        CurrentRunwayMode);
                    
                    _flights.Clear();
                    _flights.AddRange(flights);
                }
                finally
                {
                    _semaphore.Release();
                }
            
                await _mediator.Publish(new SequenceModifiedNotification(this), cancellationToken);
            }
            catch (Exception exception)
            {
                // TODO: Log
            }
            finally
            {
                await Task.Delay(TimeSpan.FromSeconds(calculationIntervalSeconds), cancellationToken);
            }
        }
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

    void AssignFeeder(Flight flight)
    {
        var feederFix = flight.Estimates.LastOrDefault(f => FeederFixes.Contains(f.FixIdentifier));
        if (feederFix is not null)
            flight.SetFeederFix(feederFix.FixIdentifier, feederFix.Estimate);
    }

    void AssignRunway(Flight _)
    {
        // TODO: Implement automatic runway assignment
        // TODO: Account for runway mode changes
    }

    void CalculateEstimates(Flight flight)
    {
        if ((flight.State > State.Stable && flight.ScheduledLandingTime < _clock.UtcNow()) ||
            flight.State == State.Frozen)
            return;

        var feederFixEstimate = _estimateProvider.GetFeederFixEstimate(flight);
        if (feederFixEstimate is not null)
            flight.UpdateFeederFixEstimate(feederFixEstimate.Value);
        
        var landingEstimate = _estimateProvider.GetLandingEstimate(flight);
        if (landingEstimate is not null)
            flight.UpdateLandingEstimate(landingEstimate.Value);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sequenceTask is not null)
            await CastAndDispose(_sequenceTask);
        
        if (_sequenceTaskCancellationSource is not null)
            await CastAndDispose(_sequenceTaskCancellationSource);
    
        await CastAndDispose(_semaphore);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}