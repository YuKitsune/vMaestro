using Maestro.Core.Connectivity;
using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Tests.Mocks;

/// <summary>
/// A mock connection manager that simulates a slave (non-master) connection.
/// All requests should be relayed to the master.
/// </summary>
public class MockSlaveConnectionManager : IMaestroConnectionManager
{
    readonly MockSlaveConnection _connection = new();

    public MockSlaveConnection Connection => _connection;

    public Task<IMaestroConnection> CreateConnection(string airportIdentifier, string partition, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConnection(string airportIdentifier, out IMaestroConnection? connection)
    {
        connection = _connection;
        return true;
    }

    public Task RemoveConnection(string airportIdentifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// A mock connection that represents a slave (non-master) connection.
/// This connection is always connected but never the master.
/// </summary>
public class MockSlaveConnection : IMaestroConnection
{
    readonly List<object> _invokedRequests = new();

    public bool IsConnected => true;
    public bool IsMaster => false;
    public Role Role => Role.Flow;

    /// <summary>
    /// Gets all requests that have been relayed through this connection.
    /// </summary>
    public IReadOnlyList<object> InvokedRequests => _invokedRequests;

    public Task Start(string callsign, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task Stop(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task Invoke<T>(T message, CancellationToken cancellationToken) where T : class, IRequest
    {
        _invokedRequests.Add(message);
        return Task.CompletedTask;
    }

    public Task Send<T>(T message, CancellationToken cancellationToken) where T : class, INotification
    {
        throw new NotImplementedException();
    }
}
