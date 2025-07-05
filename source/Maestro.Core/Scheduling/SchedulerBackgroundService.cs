using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Scheduling;

public class SchedulerBackgroundService(
    ISequenceProvider sequenceProvider,
    IMediator mediator,
    IScheduler scheduler,
    SequenceCleaner sequenceCleaner,
    IClock clock,
    ILogger logger)
{
    readonly SemaphoreSlim _semaphore = new(1, 1);
    readonly IDictionary<string, (Task, CancellationTokenSource)> _sequences = new Dictionary<string, (Task, CancellationTokenSource)>();

    public async Task Start(string airportIdentifier, CancellationToken cancellationToken = default)
    {
        logger.Information("Starting sequence for {AirportIdentifier}", airportIdentifier);
        
        using (await _semaphore.LockAsync(cancellationToken))
        {
            if (_sequences.ContainsKey(airportIdentifier))
                throw new MaestroException($"Sequence for {airportIdentifier} has already started.");
            
            var taskLogger = logger.ForContext("Sequence", airportIdentifier);
            var cancellationTokenSource = new CancellationTokenSource();
            var task = DoSequence(
                airportIdentifier,
                sequenceProvider,
                mediator,
                scheduler,
                sequenceCleaner,
                clock,
                taskLogger,
                cancellationTokenSource.Token);
            
            _sequences.Add(airportIdentifier, (task, cancellationTokenSource));
            
            logger.Debug("Started sequence for {AirportIdentifier}", airportIdentifier);
        }
    }
    
    public async Task Stop(string airportIdentifier, CancellationToken cancellationToken = default)
    {
        logger.Information("Stopping sequence for {AirportIdentifier}", airportIdentifier);
        
        using (await _semaphore.LockAsync(cancellationToken))
        {
            if (!_sequences.TryGetValue(airportIdentifier, out var tuple))
                throw new MaestroException($"Sequence for {airportIdentifier} has not been started.");
            
            var (task, cancellationTokenSource) = tuple;
            
            cancellationTokenSource.Cancel();
            await task;

            _sequences.Remove(airportIdentifier);
            
            logger.Debug("Stopped sequence for {AirportIdentifier}", airportIdentifier);
        }
    }

    static async Task DoSequence(
        string airportIdentifier,
        ISequenceProvider sequenceProvider,
        IMediator mediator,
        IScheduler scheduler,
        SequenceCleaner sequenceCleaner,
        IClock clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger.Debug("Waiting for lock on {AirportIdentifier}", airportIdentifier);
                using var lockedSequence = await sequenceProvider.GetSequence(airportIdentifier, cancellationToken);
                logger.Debug("Lock on {AirportIdentifier} acquired", airportIdentifier);

                if (lockedSequence.Sequence.NextRunwayMode is not null &&
                    lockedSequence.Sequence.RunwayModeChangeTime <= clock.UtcNow())
                {
                    var nextRunwayMode = lockedSequence.Sequence.NextRunwayMode;
                    lockedSequence.Sequence.ChangeRunwayMode(nextRunwayMode);
                    logger.Information(
                        "Runway mode for {AirportIdentifier} changed to {RunwayModeIdentifier}",
                        airportIdentifier,
                        nextRunwayMode.Identifier);
                    
                    await mediator.Publish(
                        new RunwayModeChangedNotification(
                            airportIdentifier,
                            nextRunwayMode.ToMessage(),
                            null,
                            default),
                        cancellationToken);
                }
                
                logger.Information("Scheduling {AirportIdentifier}", airportIdentifier);
                lockedSequence.Sequence.Schedule(scheduler);
                logger.Debug("Completed scheduling {AirportIdentifier}", airportIdentifier);
                
                logger.Debug("Cleaning {AirportIdentifier}", airportIdentifier);
                sequenceCleaner.CleanUpFlights(lockedSequence.Sequence);
                logger.Debug("Completed cleaning {AirportIdentifier}", airportIdentifier);
                
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Ignored
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Error scheduling {AirportIdentifier}.", airportIdentifier);
            }
        }
    }
}