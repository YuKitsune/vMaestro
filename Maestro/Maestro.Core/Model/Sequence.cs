using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Dtos.Messages;
using Maestro.Core.Infrastructure;
using MediatR;

namespace Maestro.Core.Model;

public class Sequence : IAsyncDisposable
{
    readonly IMediator _mediator;
    readonly AirportConfigurationDto _airportConfiguration;
    readonly ISeparationRuleProvider _separationRuleProvider;
    readonly IPerformanceLookup _performanceLookup;
    readonly IClock _clock;
    
    readonly List<Flight> _pending = new();
    readonly List<Flight> _flights = new();
    readonly List<BlockoutPeriod> _blockoutPeriods = new();
    readonly SemaphoreSlim _semaphore = new(1, 1);
    CancellationTokenSource? _sequenceTaskCancellationSource;
    Task? _sequenceTask;
    
    public string AirportIdentifier => _airportConfiguration.Identifier;
    public string[] FeederFixes => _airportConfiguration.FeederFixes;
    
    public IReadOnlyList<Flight> Flights => _flights.AsReadOnly();
    public RunwayModeConfigurationDto CurrentRunwayMode { get; }

    public Sequence(
        AirportConfigurationDto airportConfiguration,
        ISeparationRuleProvider separationRuleProvider,
        IPerformanceLookup performanceLookup,
        IMediator mediator,
        IClock clock)
    {
        _airportConfiguration = airportConfiguration;
        _separationRuleProvider = separationRuleProvider;
        _performanceLookup = performanceLookup;
        _mediator = mediator;
        _clock = clock;

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
            
            // TODO: Additional validation
            _flights.Add(flight);
            
            await _mediator.Publish(new SequenceModifiedNotification(this.ToDto()), cancellationToken);
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
            
            await _mediator.Publish(new SequenceModifiedNotification(this.ToDto()), cancellationToken);
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
                // TODO: Calculate ETA_FF
                // TODO: Calculate runway and terminal trajectories
                // TODO: Calculate delay and mode computations
                // TODO: Optimisation
                // TODO: Remove completed flights after a certain time

                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    // BUG: Flights on the ground at other airports are being put in the front of the sequence
                    var flights = ComputeSequence(_flights);
                    _flights.Clear();
                    _flights.AddRange(flights);
                }
                finally
                {
                    _semaphore.Release();
                }
            
                await _mediator.Publish(new SequenceModifiedNotification(this.ToDto()), cancellationToken);
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

    BlockoutPeriod? FindBlockoutPeriodAt(DateTimeOffset dateTimeOffset, string runway)
    {
        return _blockoutPeriods.FirstOrDefault(bp => bp.RunwayIdentifier == runway && bp.StartTime < dateTimeOffset && bp.EndTime > dateTimeOffset);
    }

    public Flight[] ComputeSequence(IReadOnlyList<Flight> flights)
    {
        var sequence = new List<Flight>();
        // var maxDelayPass = new List<Flight>();
        var frozenPass = new List<Flight>();
        
        foreach (var flight in flights)
        {
            switch (flight.State)
            {
                case State.Frozen:
                case State.SuperStable:
                    frozenPass.Add(flight);
                    break;
                
                case State.Stable:
                case State.Unstable:
                    if (sequence.Count == 0)
                    {
                        BlockoutPeriod? blockoutPeriod = null;
                        if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
                            blockoutPeriod = FindBlockoutPeriodAt(
                                flight.EstimatedLandingTime,
                                flight.AssignedRunwayIdentifier);
                        
                        if (blockoutPeriod is null)
                        {
                            flight.SetFlowControls(FlowControls.ProfileSpeed);

                            CalculateEstimates(flight);
                            
                            if (flight.EstimatedFeederFixTime.HasValue)
                                flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
                            
                            flight.SetLandingTime(flight.EstimatedLandingTime);
                        }
                        else
                        {
                            // Set the landing time to the end of the blockout period
                            
                            if (flight.EstimatedFeederFixTime.HasValue)
                            {
                                // TODO: Calculate STAR ETI
                                var feederFixTime = blockoutPeriod.EndTime -
                                                    (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value);
                                flight.SetFeederFixTime(feederFixTime);
                            }
                            
                            flight.SetLandingTime(blockoutPeriod.EndTime);
                        }
                    }
                    else
                    {
                        var leadingFlight = sequence.Last();
                        var landingTime = leadingFlight.ScheduledLandingTime + _separationRuleProvider.GetRequiredSpacing(leadingFlight, flight, CurrentRunwayMode);
                        
                        BlockoutPeriod? blockoutPeriod = null;
                        if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
                            blockoutPeriod = FindBlockoutPeriodAt(
                                flight.EstimatedLandingTime,
                                flight.AssignedRunwayIdentifier);
                        
                        // Inside blockout period, push back landing time
                        var earliestLandingTime = blockoutPeriod?.EndTime ?? landingTime;
                        if (blockoutPeriod is not null)
                        {
                            // TODO: Re-calculate runway assignment when the runway mode is going to change
                            AssignFeeder(flight);
                            AssignRunway(flight);
                            CalculateEstimates(flight);
                        }

                        // Don't speed the flight up to make the earliest landing time
                        if (earliestLandingTime < flight.EstimatedLandingTime)
                        {
                            if (flight.EstimatedFeederFixTime.HasValue)
                                flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
                            
                            flight.SetLandingTime(flight.EstimatedLandingTime);
                            flight.SetFlowControls(FlowControls.ProfileSpeed);
                            CalculateEstimates(flight);
                            break;
                        }

                        // if (flight.MaxDelay < TimeSpan.MaxValue && flight.EstimatedLandingTime != earliestLandingTime)//insert these after first pass
                        // {
                        //     maxDelayPass.Add(flight);
                        //     break;
                        // }

                        // If we're here, there is a delay
                        var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
                        if (performanceData.IsJet)
                        {
                            flight.SetFlowControls(FlowControls.S250);
                            CalculateEstimates(flight);
                        }
                        
                        if (flight.EstimatedFeederFixTime.HasValue)
                            flight.SetFeederFixTime(landingTime + (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value));
                        
                        flight.SetLandingTime(landingTime);
                    }
                            
                    sequence.Add(flight);
                    break;
            }
        }
        
        // foreach (var flight in maxDelayPass.OrderBy(f => f.EstimatedLandingTime).ThenBy(f => f.TotalDelayToRunway))
        // {
        //     var i = sequence.FindLastIndex(f =>
        //         flight.ScheduledLandingTime + GetRequiredSpacing(f, flight) - flight.EstimatedLandingTime < flight.MaxDelay + GetRequiredSpacing(f, flight)) + 1;
        //     sequence.Insert(i, flight);
        //     
        //     flight.SetLandingTime(flight.EstimatedLandingTime);
        //     if (flight.EstimatedFeederFixTime.HasValue)
        //         flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
        //     
        //     DoSequencePass(i, sequence);
        // }

        // TODO: Confirm what this does
        foreach (var flight in frozenPass.OrderBy(f => f.ScheduledLandingTime))
        {
            var orderedTimes = flights.OrderBy(k => k.ScheduledLandingTime).ToList();
            
            // var i = sequence.FindLastIndex(f => (f.State > Flight.States.Stable && sequenceTimes[f][0] < flight.STA) || (sequenceTimes[f][0] + GetRequiredSpacing(airport, f, flight) < flight.STA)) + 1;
            var leader = orderedTimes.FindLast(k => 
                (k.State > State.Stable && k.ScheduledLandingTime < flight.ScheduledLandingTime) ||
                (k.ScheduledLandingTime + _separationRuleProvider.GetRequiredSpacing(k, flight, CurrentRunwayMode) < flight.ScheduledLandingTime));
            
            var i = 0;
            if (leader is not null)
                i = sequence.IndexOf(leader) + 1;
            
            sequence.Insert(i, flight);
            // sequenceTimes.Add(flight, new DateTime[2] { flight.STA, flight.STA_FF });
            
            DoSequencePass(i, sequence);
        }

        return sequence.ToArray();
    }

    void DoSequencePass(int position, List<Flight> sequence)
    {
        for (var i = position; i < sequence.Count; i++)
        {
            var flight = sequence[i];
            
            // Do not alter STA for anything SuperStable or beyond
            if (flight.State > State.Stable)
                continue;

            if (i == 0)
            {
                var landingTime = flight.EstimatedLandingTime;
                
                BlockoutPeriod? blockoutPeriod = null;
                if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
                    blockoutPeriod = FindBlockoutPeriodAt(
                        flight.EstimatedLandingTime,
                        flight.AssignedRunwayIdentifier);
                
                if (blockoutPeriod is not null)
                {
                    // TODO:
                    // if (f.ModeProcessed != airport.FutureMode)
                    // {
                    //     AssignFeeder(airport, f);
                    //     AssignRunway(airport, f);
                    //     CalculateEstimates(airport, f);
                    // }
                    
                    AssignFeeder(flight);
                    AssignRunway(flight);
                    CalculateEstimates(flight);
                    
                    landingTime = blockoutPeriod.EndTime;
                }

                if (landingTime == flight.EstimatedLandingTime)
                {
                    flight.SetLandingTime(landingTime);
                    
                    if (flight.EstimatedFeederFixTime.HasValue)
                        flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
                }
                else
                {
                    var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
                    if (performanceData.IsJet)
                    {
                        flight.SetFlowControls(FlowControls.S250);
                        CalculateEstimates(flight);
                    }
                    
                    flight.SetLandingTime(landingTime);
                    
                    if (flight.EstimatedFeederFixTime.HasValue)
                        flight.SetFeederFixTime(landingTime + (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value));
                }
            }
            else
            {
                var leader = sequence[i - 1];
                
                var landingTime = leader.EstimatedLandingTime + _separationRuleProvider.GetRequiredSpacing(leader, flight, CurrentRunwayMode);
                
                BlockoutPeriod? blockoutPeriod = null;
                if (!string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
                    blockoutPeriod = FindBlockoutPeriodAt(
                        flight.EstimatedLandingTime,
                        flight.AssignedRunwayIdentifier);
                
                if (blockoutPeriod is not null)
                {
                    // TODO:
                    // if (f.ModeProcessed != airport.FutureMode)
                    // {
                    //     AssignFeeder(airport, f);
                    //     AssignRunway(airport, f);
                    //     CalculateEstimates(airport, f);
                    // }
                    
                    AssignFeeder(flight);
                    AssignRunway(flight);
                    CalculateEstimates(flight);
                    
                    landingTime = blockoutPeriod.EndTime;
                }

                if (landingTime <= flight.EstimatedLandingTime)
                {
                    flight.SetLandingTime(landingTime);
                    
                    if (flight.EstimatedFeederFixTime.HasValue)
                        flight.SetFeederFixTime(landingTime + (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value));
                    
                    // There are no more changes to process if this time is just ETA.
                    if (i != 1)
                        break;
                }
                else
                {
                    var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
                    if (performanceData.IsJet)
                    {
                        flight.SetFlowControls(FlowControls.S250);
                        CalculateEstimates(flight);
                    }
                    
                    flight.SetLandingTime(landingTime);
                    
                    if (flight.EstimatedFeederFixTime.HasValue)
                        flight.SetFeederFixTime(landingTime + (flight.EstimatedLandingTime - flight.EstimatedFeederFixTime.Value));
                }
            }
        }
    }

    void AssignFeeder(Flight flight)
    {
        var feederFix = flight.Estimates.LastOrDefault(f => FeederFixes.Contains(f.FixIdentifier));
        if (feederFix is not null)
            flight.SetFeederFix(feederFix.FixIdentifier, feederFix.Estimate);
    }

    void AssignRunway(Flight flight)
    {
        // TODO: Account for runway mode changes
        var runwayMode = CurrentRunwayMode;
        
        // TODO: Account for manual runway selection
        // if (flight.ManualRunway)
        //     return;
        
        var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        var candidates = new List<RunwayAssignmentRuleDto>();
        
        foreach (var runwayAssignmentRule in runwayMode.AssignmentRules)
        {
            var typeMatch = performanceData.IsJet == runwayAssignmentRule.Jets
                            || !performanceData.IsJet == runwayAssignmentRule.NonJets;
            
            var wakeMatch = (runwayAssignmentRule.Heavy && performanceData.WakeCategory is WakeCategory.Heavy or WakeCategory.SuperHeavy)
                || (runwayAssignmentRule.Medium && performanceData.WakeCategory is WakeCategory.Medium)
                || (runwayAssignmentRule.Light && performanceData.WakeCategory is WakeCategory.Light)
                || runwayAssignmentRule is not { Heavy: true, Medium: true, Light: true };

            var feederMatch = false;
            if (runwayAssignmentRule.FeederFixes.Any() && !string.IsNullOrEmpty(flight.FeederFixIdentifier))
                feederMatch = runwayAssignmentRule.FeederFixes.Contains(flight.FeederFixIdentifier);
            
            if (typeMatch && feederMatch && wakeMatch)
                candidates.Add(runwayAssignmentRule);
        }

        if (!candidates.Any())
            return;

        if (candidates.Count == 1)
        {
            flight.SetRunway(candidates.Single().RunwayIdentifier);
            return;
        }
        
        // Try assigning the highest priority
        var topPriority = candidates.Min(c => c.Priority);
        candidates.RemoveAll(r => r.Priority != topPriority);
        if (candidates.Count == 1)
        {
            flight.SetRunway(candidates.Single().RunwayIdentifier);
            return;
        }
        
        // Defer to runway direction
        if (string.IsNullOrEmpty(flight.FeederFixIdentifier) && flight.LastKnownPosition.HasValue)
        {
            if (flight.Estimates.Length == 0)
            {
                flight.SetRunway(candidates.First().RunwayIdentifier);
                return;
            }

            var airportCoords = flight.Estimates.Last().Coordinate;
            
            var track = Calculations.CalculateTrack(flight.LastKnownPosition.Value.ToCoordinate(), airportCoords);
            
            var delta = double.MaxValue;
            var winner = candidates.First();
            foreach(var rule in candidates)
            {
                // Approximate the runway heading using the first two digits
                if (rule.RunwayIdentifier.Length < 2 ||
                    !int.TryParse(rule.RunwayIdentifier.Substring(0, 2), out var approximateRunwayHeading))
                    continue;
                
                approximateRunwayHeading *= 10;
                double test = Math.Abs(track - approximateRunwayHeading);
                if (test < delta)
                {
                    delta = test;
                    winner = rule;
                }
            }
            
            flight.SetRunway(winner.RunwayIdentifier);
        }

        // Defer to track miles
        var distanceWinner = candidates.First();
        var shortest = int.MaxValue;
        foreach (var rule in candidates)
        {
            var trackMiles = GetTrackMilesToRunway(flight.AssignedStarIdentifier, flight.AssignedRunwayIdentifier);

            if (trackMiles <= 0)
                continue;
            
            if (trackMiles < shortest)
            {
                distanceWinner = rule;
                shortest = trackMiles;
            }
        }

        flight.SetRunway(distanceWinner.RunwayIdentifier);
    }

    void CalculateEstimates(Flight flight)
    {
        if (flight.Estimates.Length == 0 ||
            (flight.State > State.Stable && flight.ScheduledLandingTime < _clock.UtcNow()) ||
            flight.State == State.Frozen)
            return;

        FixEstimate? feederFix = null;
        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier))
            feederFix = flight.Estimates.FirstOrDefault(f => f.FixIdentifier == flight.FeederFixIdentifier);

        if (feederFix is null)
        {
            // Use system estimate for landing time if no feeder fix exists
            flight.UpdateLandingEstimate(flight.Estimates.Last().Estimate);
        }
        else
        {
            // When a feeder fix does exist, calculate the landing time based on ETA_FF + STAR ETI
            
            var feederFixEstimate = DateTimeOffset.MaxValue;
            if (feederFix.Estimate != DateTimeOffset.MaxValue)
            {
                // Use system estimate if available for ETA_FF
                feederFixEstimate = feederFix.Estimate;
            }
            else if (flight.LastKnownPosition is not null && flight.LastKnownPosition.Value.VerticalTrack != VerticalTrack.Climbing && flight.LastKnownPosition.Value.GroundSpeed > 60)
            {
                // Otherwise, calculate ETA_FF using the last known position and speed
                var dist = Calculations.CalculateDistanceNauticalMiles(flight.LastKnownPosition.Value.ToCoordinate(), feederFix.Coordinate);

                var radar = DateTime.UtcNow + TimeSpan.FromHours(dist / flight.LastKnownPosition.Value.GroundSpeed);

                if (dist < 50 || feederFixEstimate == DateTime.MaxValue)
                {
                    feederFixEstimate = radar;
                }
                else if (dist < 120)
                {
                    var delta = feederFixEstimate - radar;
                    feederFixEstimate = radar + TimeSpan.FromMinutes(delta.TotalMinutes / 2.0);// take average
                }
            }

            // Calculate STAR ETI based on track miles and aircraft trajectory and performance
            var trackMiles = 0;
            if (!string.IsNullOrEmpty(flight.AssignedStarIdentifier) && !string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
            {
                trackMiles = GetTrackMilesToRunway(flight.AssignedRunwayIdentifier, flight.AssignedRunwayIdentifier);
            }
            
            flight.UpdateFeederFixEstimate(feederFixEstimate);
            
            // Use system estimate for landing time if no trajectory data exists
            if (trackMiles <= 0 || flight.Trajectory.Length == 0 || feederFixEstimate == DateTime.MaxValue)
            {
                flight.UpdateLandingEstimate(flight.Estimates.Last().Estimate);
                return;
            }
            
            var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
            var maxDescentSpeed = performanceData.GetDescentSpeedAt(20000);

            var takeDistance = 0d;
            var i = 1;
            var eet = TimeSpan.Zero;
            while (takeDistance < trackMiles && i < flight.Trajectory.Length)
            {
                var lastPos = flight.Trajectory[flight.Trajectory.Length - i];
                var nextPos = flight.Trajectory[flight.Trajectory.Length - (++i)];

                takeDistance += Calculations.CalculateDistanceNauticalMiles(lastPos.Position, nextPos.Position);
                eet = eet.Add(lastPos.Interval);

                if (!performanceData.IsJet)
                    continue;

                switch(flight.FlowControls)
                {
                    case FlowControls.MaxSpeed:
                        if(lastPos.Altitude > 6000)//increase speeds to cancel 250 knot restriction
                        {
                            var profileSpeed = performanceData.GetDescentSpeedAt(lastPos.Altitude);
                            if(maxDescentSpeed > profileSpeed)
                            {
                                var ratio = maxDescentSpeed / profileSpeed;
                                var seconds = lastPos.Interval.TotalSeconds - (lastPos.Interval.TotalSeconds * ratio);
                                eet = eet.Add(TimeSpan.FromSeconds(seconds)); // remove the difference
                            }
                        }
                        break;
                    
                    case FlowControls.S250:
                        if(lastPos.Altitude > 10000)
                        {
                            var profileSpeed = performanceData.GetDescentSpeedAt(lastPos.Altitude);
                            if(profileSpeed > 250)
                            {
                                var ratio = profileSpeed / 250.0;
                                var seconds = (lastPos.Interval.TotalSeconds * ratio) - lastPos.Interval.TotalSeconds;
                                eet = eet.Add(TimeSpan.FromSeconds(seconds));
                            }
                        }
                        break;
                }
            }

            flight.UpdateLandingEstimate(feederFixEstimate + eet);
        }
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

    int GetTrackMilesToRunway(string arrivalIdentifier, string runwayIdentifier)
    {
        throw new NotImplementedException();
    }
}