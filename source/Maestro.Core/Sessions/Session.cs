using Maestro.Core.Infrastructure;
using Maestro.Core.Model;

namespace Maestro.Core.Sessions;

// TODO: Refactor this so that the Session does not directly manage the MaestroConnection
// - Make MaestroConnection an IObservable that the Session can subscribe to

public interface ISession
{
    string AirportIdentifier { get; }
    Sequence Sequence { get; }
    string Position { get; }
    MaestroConnection? Connection { get; }
    SemaphoreSlim Semaphore { get; }
    ConnectionInfo? ConnectionInfo { get; }
    bool OwnsSequence { get; }
    bool IsActive { get; }
    bool IsConnected { get; }

    Task Start(string position, CancellationToken cancellationToken);
    Task Stop(CancellationToken cancellationToken);

    Task SetConnectionInfo(ConnectionInfo connectionInfo, CancellationToken cancellationToken);
    Task Disconnect(CancellationToken cancellationToken);

    Task TakeOwnership(CancellationToken cancellationToken);
    Task RevokeOwnership(CancellationToken cancellationToken);
}

public class Session : ISession, IAsyncDisposable
{
    readonly IMaestroConnectionFactory _maestroConnectionFactory;

    public string AirportIdentifier => Sequence.AirportIdentifier;
    public Sequence Sequence { get; private set; }
    public ConnectionInfo? ConnectionInfo { get; private set; }
    public MaestroConnection? Connection { get; private set; }
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public string Position { get; private set; } = string.Empty;
    public bool OwnsSequence { get; private set; } = true;
    public bool IsActive { get; private set; }
    public bool IsConnected => Connection?.IsConnected ?? false;

    public Session(Sequence sequence, IMaestroConnectionFactory maestroConnectionFactory)
    {
        _maestroConnectionFactory = maestroConnectionFactory;

        Sequence = sequence;
    }

    public async Task Start(string position, CancellationToken cancellationToken)
    {
        Position = position;
        if (ConnectionInfo is not null)
        {
            await Connect(ConnectionInfo, position, cancellationToken);
        }

        IsActive = true;
    }

    public async Task SetConnectionInfo(ConnectionInfo connectionInfo, CancellationToken cancellationToken)
    {
        ConnectionInfo = connectionInfo;

        if (IsActive)
            await Connect(connectionInfo, Position, cancellationToken);
    }

    async Task Connect(ConnectionInfo connectionInfo, string position, CancellationToken cancellationToken)
    {
        var maestroConnection = _maestroConnectionFactory.Create(
            connectionInfo.Partition,
            AirportIdentifier,
            position);

        await maestroConnection.Start(cancellationToken);
        Connection = maestroConnection;

        // Server will send a ConnectionInitialized message to us when we've successfully connected
    }

    public async Task Disconnect(CancellationToken cancellationToken)
    {
        if (Connection is not null && Connection.IsConnected)
            await Connection.Stop(cancellationToken);

        Connection = null;
        ConnectionInfo = null;

        // When disconnecting from server, revert to offline mode where all roles can own sequences
        await TakeOwnership(cancellationToken);
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        if (Connection is not null && Connection.IsConnected)
            await Connection.Stop(cancellationToken);

        IsActive = false;
    }

    public Task TakeOwnership(CancellationToken cancellationToken)
    {
        OwnsSequence = true;
        return Task.CompletedTask;
    }

    public Task RevokeOwnership(CancellationToken cancellationToken)
    {
        OwnsSequence = false;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Connection != null)
            await Connection.DisposeAsync();
        Semaphore.Dispose();
    }
}
