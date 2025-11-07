using System.Diagnostics.CodeAnalysis;
using Maestro.Core.Configuration;

namespace Maestro.Server;

public interface IConnectionManager
{
    Connection Add(string connectionId, string partition, string airportIdentifier, string callsign, Role role);

    bool TryGetConnection(
        string connectionId,
        [NotNullWhen(true)] out Connection? connection);

    Connection[] GetPeers(Connection connection);
    Connection[] GetConnections(string partition, string airportIdentifier);
    Connection[] GetAllConnections();
    void Remove(Connection connection);
}

public class ConnectionManager : IConnectionManager
{
    readonly List<Connection> _connections = [];

    public Connection Add(string connectionId, string partition, string airportIdentifier, string callsign, Role role)
    {
        if (_connections.Any(c => c.Id == connectionId))
            throw new InvalidOperationException($"Connection {connectionId} already exists");

        var connection = new Connection(connectionId, partition, airportIdentifier, callsign, role);
        _connections.Add(connection);
        return connection;
    }

    public bool TryGetConnection(
        string connectionId,
        [NotNullWhen(true)] out Connection? connection)
    {
        connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        return connection is not null;
    }

    public Connection[] GetPeers(Connection connection)
    {
        return _connections
            .Where(c => c.Id != connection.Id && c.Partition == connection.Partition && c.AirportIdentifier == connection.AirportIdentifier)
            .ToArray();
    }

    public Connection[] GetConnections(string partition, string airportIdentifier)
    {
        return _connections
            .Where(c => c.Partition == partition && c.AirportIdentifier == airportIdentifier)
            .ToArray();
    }

    public Connection[] GetAllConnections()
    {
        return [.. _connections];
    }

    public void Remove(Connection connection)
    {
        _connections.Remove(connection);
    }
}
