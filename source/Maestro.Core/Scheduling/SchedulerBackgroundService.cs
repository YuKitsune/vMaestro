using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Serilog;

namespace Maestro.Core.Scheduling;

public class SchedulerBackgroundService(
    ISequenceProvider sequenceProvider,
    IMessageDispatcher messageDispatcher,
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
                messageDispatcher,
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
        IMessageDispatcher messageDispatcher,
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
                using (var lockedSequence = await sequenceProvider.GetSequence(airportIdentifier, cancellationToken))
                {
                    logger.Debug("Lock on {AirportIdentifier} acquired", airportIdentifier);

                    lockedSequence.Sequence.TrySwapRunwayModes(clock);

                    logger.Information("Scheduling {AirportIdentifier}", airportIdentifier);
                    scheduler.Schedule(lockedSequence.Sequence);
                    logger.Debug("Completed scheduling {AirportIdentifier}", airportIdentifier);

                    logger.Debug("Updating flight states for {AirportIdentifier}", airportIdentifier);
                    foreach (var flight in lockedSequence.Sequence.Flights)
                    {
                        flight.UpdateStateBasedOnTime(clock);
                    }
                    logger.Debug("Completed updating flight states for {AirportIdentifier}", airportIdentifier);

                    logger.Debug("Cleaning {AirportIdentifier}", airportIdentifier);
                    sequenceCleaner.CleanUpFlights(lockedSequence.Sequence);
                    logger.Debug("Completed cleaning {AirportIdentifier}", airportIdentifier);

                    await messageDispatcher.Send(
                        new SequenceUpdatedNotification(
                            lockedSequence.Sequence.AirportIdentifier,
                            lockedSequence.Sequence.ToMessage()),
                        cancellationToken);
                }

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
