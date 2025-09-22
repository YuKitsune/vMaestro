using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Serilog;

namespace Maestro.Core.Sessions;

public interface ISession
{
    string AirportIdentifier { get; }
    Sequence Sequence { get; }
    MaestroConnection? Connection { get; }
    SemaphoreSlim Semaphore { get; }
    bool OwnsSequence { get; }
    Role Role { get; }
    bool IsActive { get; }
    bool IsConnected { get; }

    Task Start(string position, CancellationToken cancellationToken);
    Task Stop(CancellationToken cancellationToken);

    Task Connect(MaestroConnection maestroConnection, CancellationToken cancellationToken);
    Task Disconnect(CancellationToken cancellationToken);

    Task TakeOwnership(CancellationToken cancellationToken);
    Task RevokeOwnership(CancellationToken cancellationToken);
}

public class Session : ISession, IAsyncDisposable
{
    readonly ILogger _logger;

    public string AirportIdentifier => Sequence.AirportIdentifier;
    public Sequence Sequence { get; private set; }
    public MaestroConnection? Connection { get; private set; }
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public string Position { get; private set; } = string.Empty;
    public Role Role { get; private set; } = Role.Observer;
    public bool OwnsSequence { get; private set; } = true;
    public bool IsActive { get; private set; }
    public bool IsConnected => Connection?.IsConnected ?? false;

    readonly BackgroundTask _schedulerTask;
    // readonly BackgroundTask _synchronizeTask;

    public Session(Sequence sequence, ILogger logger)
    {
        _logger = logger;
        _schedulerTask = new BackgroundTask(SchedulerLoop);
        // _synchronizeTask = new BackgroundTask(SynchronizeLoop);

        Sequence = sequence;
    }

    public async Task Start(string position, CancellationToken cancellationToken)
    {
        Position = position;
        if (Connection is not null)
        {
            await ConnectInternal(Connection, cancellationToken);
        }

        if (OwnsSequence)
            await StartOwnershipTasks(cancellationToken);

        IsActive = true;
    }

    public async Task Connect(MaestroConnection maestroConnection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(Position))
            throw new MaestroException("Cannot connect to a sequence without a position.");

        if (Connection is not null && Connection.IsConnected)
            await Disconnect(cancellationToken);

        // If we're not active yet, don't start the connection
        // Let the Start method handle that
        if (!IsActive)
            return;

        await ConnectInternal(maestroConnection, cancellationToken);
    }

    async Task ConnectInternal(MaestroConnection maestroConnection, CancellationToken cancellationToken)
    {
        try
        {
            var result = await maestroConnection.Start(Position, cancellationToken);
            Connection = maestroConnection;
            Role = result.Role;
            OwnsSequence = result.OwnsSequence;

            if (result.Sequence is not null)
                Sequence.Restore(result.Sequence);

            if (!OwnsSequence)
                await StopOwnershipTasks(cancellationToken);
        }
        catch
        {
            Connection = null;
            Role = Role.Flow;
            OwnsSequence = true;
            await StartOwnershipTasks(cancellationToken);
            throw;
        }
    }

    public async Task Disconnect(CancellationToken cancellationToken)
    {
        if (Connection is not null && Connection.IsConnected)
            await Connection.Stop(cancellationToken);

        // When disconnecting from server, revert to offline mode where all roles can own sequences
        await TakeOwnership(cancellationToken);
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        if (Connection is not null && Connection.IsConnected)
            await Connection.Stop(cancellationToken);

        if (_schedulerTask.IsRunning)
            await _schedulerTask.Stop(cancellationToken);

        // if (_synchronizeTask.IsRunning)
        //     await _synchronizeTask.Stop(cancellationToken);

        IsActive = false;
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
                // await _mediator.Send(new ScheduleRequest(Sequence.AirportIdentifier), cancellationToken);
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
        if (Connection != null)
            await Connection.DisposeAsync();
        await _schedulerTask.DisposeAsync();
        // await _synchronizeTask.DisposeAsync();
        Semaphore.Dispose();
    }
}
