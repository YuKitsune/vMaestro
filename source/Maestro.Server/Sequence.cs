using Maestro.Core.Configuration;
using Maestro.Core.Messages;

namespace Maestro.Server;

public class Sequence(GroupKey groupKey)
{
    private readonly List<Connection> _connections = [];
    private readonly object _lock = new();

    public GroupKey GroupKey { get; } = groupKey;
    public SequenceMessage? LatestSequence { get; set; }
    public IReadOnlyDictionary<string, Role[]>? Permissions { get; set; }

    public IReadOnlyList<Connection> Connections
    {
        get
        {
            lock (_lock)
            {
                return _connections.ToList();
            }
        }
    }

    public Connection AddConnection(string connectionId, string callsign, Role role)
    {
        lock (_lock)
        {
            var connection = new Connection(connectionId, GroupKey, callsign, role);
            _connections.Add(connection);

            return connection;
        }
    }

    public void RemoveConnection(string connectionId)
    {
        lock (_lock)
        {
            var connection = _connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return;

            _connections.Remove(connection);
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _connections.Count == 0;
            }
        }
    }
}
