using System.Diagnostics.CodeAnalysis;
using Maestro.Contracts.Connectivity;

namespace Maestro.Server;

public interface IConnectionManager
{
    Connection Add(string connectionId, string version, string environment, string airportIdentifier, string callsign, Role role);

    bool TryGetConnection(
        string connectionId,
        [NotNullWhen(true)] out Connection? connection);

    Connection[] GetPeers(Connection connection);
    Connection[] GetConnections(string environment, string airportIdentifier);
    Connection[] GetAllConnections();
    void Remove(Connection connection);

    /// <summary>
    /// Atomically demotes any existing master for the same airport and promotes <paramref name="newMaster"/>.
    /// Returns the previous master, or null if there was none.
    /// </summary>
    Connection? PromoteMaster(Connection newMaster);
}

public class ConnectionManager : IConnectionManager
{
    readonly Dictionary<string, Connection> _connections = new();
    readonly object _gate = new();

    public Connection Add(string connectionId, string version, string environment, string airportIdentifier, string callsign, Role role)
    {
        lock (_gate)
        {
            if (_connections.ContainsKey(connectionId))
                throw new InvalidOperationException($"Connection {connectionId} already exists");

            var connection = new Connection(connectionId, version, environment, airportIdentifier, callsign, role);
            _connections[connectionId] = connection;
            return connection;
        }
    }

    public bool TryGetConnection(
        string connectionId,
        [NotNullWhen(true)] out Connection? connection)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(connectionId, out connection);
        }
    }

    public Connection[] GetPeers(Connection connection)
    {
        lock (_gate)
        {
            return _connections.Values
                .Where(c => c.Id != connection.Id
                    && c.Environment == connection.Environment
                    && c.AirportIdentifier == connection.AirportIdentifier)
                .ToArray();
        }
    }

    public Connection[] GetConnections(string environment, string airportIdentifier)
    {
        lock (_gate)
        {
            return _connections.Values
                .Where(c => c.Environment == environment && c.AirportIdentifier == airportIdentifier)
                .ToArray();
        }
    }

    public Connection[] GetAllConnections()
    {
        lock (_gate)
        {
            return [.. _connections.Values];
        }
    }

    public void Remove(Connection connection)
    {
        lock (_gate)
        {
            _connections.Remove(connection.Id);
        }
    }

    public Connection? PromoteMaster(Connection newMaster)
    {
        lock (_gate)
        {
            Connection? previous = null;
            foreach (var conn in _connections.Values)
            {
                if (conn.IsMaster && conn.Id != newMaster.Id
                    && conn.Environment == newMaster.Environment
                    && conn.AirportIdentifier == newMaster.AirportIdentifier)
                {
                    conn.IsMaster = false;
                    previous = conn;
                    break;
                }
            }

            newMaster.IsMaster = true;
            return previous;
        }
    }
}
