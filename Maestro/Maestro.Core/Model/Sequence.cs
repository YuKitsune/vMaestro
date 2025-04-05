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
    
    public string[] FeederFixes { get; }
    
    public IReadOnlyList<Flight> Flights => _flights.AsReadOnly();
    public IReadOnlyList<Flight> Pending => _pending.AsReadOnly();

    CancellationTokenSource recurringTaskCancellationTokenSource = new();
    Task recurringCalculationTask;

    public Sequence(
        string airportIdentifier,
        RunwayMode[] runwayModes,
        string[] feederFixes,
        IMediator mediator)
    {
        _mediator = mediator;
        AirportIdentifier = airportIdentifier;
        RunwayModes = runwayModes;
        CurrentRunwayMode = runwayModes.First();
        FeederFixes = feederFixes;
        
        recurringCalculationTask = Calculate(recurringTaskCancellationTokenSource.Token);
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

    async Task Calculate(CancellationToken cancellationToken)
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
        await recurringCalculationTask;
        await CastAndDispose(recurringCalculationTask);
        await CastAndDispose(recurringTaskCancellationTokenSource);
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
