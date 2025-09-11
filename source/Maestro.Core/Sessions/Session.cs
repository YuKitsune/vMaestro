using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions;

public interface ISession
{
    string AirportIdentifier { get; }
    Sequence Sequence { get; }
    MaestroConnection? Connection { get; }
    SemaphoreSlim Semaphore { get; }
    bool OwnsSequence { get; }

    Task Start(string position, CancellationToken cancellationToken);
    Task Stop(CancellationToken cancellationToken);

    Task TakeOwnership(CancellationToken cancellationToken);
    Task RevokeOwnership(CancellationToken cancellationToken);
}

public class Session : ISession, IAsyncDisposable
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;
    readonly IMediator _mediator;
    // readonly INotificationStream<SequenceUpdatedNotification> _sequenceUpdatedNotificationStream;
    readonly ILogger _logger;

    public string AirportIdentifier => Sequence.AirportIdentifier;
    public Sequence Sequence { get; private set; }
    public MaestroConnection? Connection { get; }
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public bool OwnsSequence { get; private set; } = true;

    readonly BackgroundTask _schedulerTask;
    // readonly BackgroundTask _synchronizeTask;

    public Session(
        IAirportConfigurationProvider airportConfigurationProvider,
        // INotificationStream<SequenceUpdatedNotification> sequenceUpdatedNotificationStream,
        IMediator mediator,
        ILogger logger,
        Sequence sequence,
        MaestroConnection? connection = null)
    {
        _airportConfigurationProvider = airportConfigurationProvider;
        _mediator = mediator;
        // _sequenceUpdatedNotificationStream = sequenceUpdatedNotificationStream;
        _logger = logger;
        _schedulerTask = new BackgroundTask(SchedulerLoop);
        // _synchronizeTask = new BackgroundTask(SynchronizeLoop);

        Sequence = sequence;
        Connection = connection;
    }

    public async Task Start(string position, CancellationToken cancellationToken)
    {
        if (Connection is not null)
        {
            var result = await Connection.Start(position, cancellationToken);
            OwnsSequence = result.OwnsSequence;

            if (result.Sequence is not null)
            {
                // YUCK
                var airportConfig = _airportConfigurationProvider
                    .GetAirportConfigurations()
                    .Single(a => a.Identifier == AirportIdentifier);
                Sequence = new Sequence(airportConfig, result.Sequence);
            }
        }

        if (OwnsSequence)
            await StartOwnershipTasks(cancellationToken);
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        if (Connection is not null && Connection.IsConnected)
            await Connection.Stop(cancellationToken);

        if (_schedulerTask.IsRunning)
            await _schedulerTask.Stop(cancellationToken);

        // if (_synchronizeTask.IsRunning)
        //     await _synchronizeTask.Stop(cancellationToken);
    }

    public async Task TakeOwnership(CancellationToken cancellationToken)
    {
        OwnsSequence = true;
        await StartOwnershipTasks(cancellationToken);
    }

    public async Task RevokeOwnership(CancellationToken cancellationToken)
    {
        OwnsSequence = false;
        await StopOwnershipTasks(cancellationToken);
    }

    async Task StartOwnershipTasks(CancellationToken cancellationToken)
    {
        if (!_schedulerTask.IsRunning)
            await _schedulerTask.Start(cancellationToken);

        // if (!_synchronizeTask.IsRunning)
        //     await _synchronizeTask.Start(cancellationToken);
    }

    async Task StopOwnershipTasks(CancellationToken cancellationToken)
    {
        if (_schedulerTask.IsRunning)
            await _schedulerTask.Stop(cancellationToken);

        // if (_synchronizeTask.IsRunning)
        //     await _synchronizeTask.Stop(cancellationToken);
    }

    async Task SchedulerLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                await _mediator.Send(new ScheduleRequest(Sequence.AirportIdentifier), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Ignored
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error scheduling {AirportIdentifier}.", AirportIdentifier);
            }
        }
    }

    // Publish SequenceUpdatedNotifications to connected clients if we own the sequence
    // async Task SynchronizeLoop(CancellationToken cancellationToken)
    // {
    //     await foreach (var notification in _sequenceUpdatedNotificationStream.SubscribeAsync(cancellationToken))
    //     {
    //         if (!OwnsSequence || notification.AirportIdentifier != AirportIdentifier || Connection is null)
    //             continue;
    //         try
    //         {
    //             _logger.Information("Synchronizing {AirportIdentifier}.", AirportIdentifier);
    //             await Connection.Send(notification, cancellationToken);
    //         }
    //         catch (Exception exception)
    //         {
    //             _logger.Error(exception, "Error synchronizing {AirportIdentifier}.", AirportIdentifier);
    //         }
    //     }
    // }

    public async ValueTask DisposeAsync()
    {
        if (Connection != null) await Connection.DisposeAsync();
        await _schedulerTask.DisposeAsync();
        // await _synchronizeTask.DisposeAsync();
    }
}
