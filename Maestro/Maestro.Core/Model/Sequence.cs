using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Dtos.Messages;
using MediatR;

namespace Maestro.Core.Model;

public class Sequence : IAsyncDisposable
{
    readonly IMediator _mediator;
    readonly List<Flight> _flights = new();
    readonly List<Flight> _pending = new();
    readonly List<BlockoutPeriod> _blockoutPeriods = new();
    readonly SemaphoreSlim _semaphore = new(1, 1);
    
    public string AirportIdentifier { get; }
    
    public RunwayMode[] RunwayModes { get; }
    public RunwayMode CurrentRunwayMode { get; }
    public RunwayMode? NextRunwayMode { get; private set; }
    public DateTimeOffset RunwayModeChangeTime { get; private set; }
    
    public FixConfigurationDto[] FeederFixes { get; }
    
    public IReadOnlyList<Flight> Flights => _flights.AsReadOnly();
    public IReadOnlyList<Flight> Pending => _pending.AsReadOnly();

    CancellationTokenSource? _sequenceTaskCancellationSource;
    Task? _sequenceTask;

    public Sequence(
        string airportIdentifier,
        RunwayMode[] runwayModes,
        FixConfigurationDto[] feederFixes,
        IMediator mediator)
    {
        _mediator = mediator;
        AirportIdentifier = airportIdentifier;
        RunwayModes = runwayModes;
        CurrentRunwayMode = runwayModes.First();
        FeederFixes = feederFixes;
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
        var calculationIntervalSeconds = 60;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // TODO: Calculate ETA_FF
                // TODO: Calculate runway and terminal trajectories
                // TODO: Calculate delay and mode computations
                // TODO: Optimisation
                // TODO: Remove completed flights after a certain time
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
