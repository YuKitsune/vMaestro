using Maestro.Core.Sessions;

namespace Maestro.Server;

public record SessionKey(string Partition, string AirportIdentifier);

public class SessionCache
{
    readonly Dictionary<SessionKey, SessionMessage> _sessions = new();

    public SessionMessage? Get(string partition, string airportIdentifier)
    {
        var key = new SessionKey(partition, airportIdentifier);
        return _sessions.GetValueOrDefault(key);
    }

    public void Set(string partition, string airportIdentifier, SessionMessage sessionMessage)
    {
        var key = new SessionKey(partition, airportIdentifier);
        _sessions[key] = sessionMessage;
    }

    // BUG: Need to remove old entries after the last client disconnects
    public void Evict(string partition, string airportIdentifier)
    {
        var key = new SessionKey(partition, airportIdentifier);
        _sessions.Remove(key);
    }

    public IEnumerable<(SessionKey Key, SessionMessage Session)> GetAll()
    {
        foreach (var (key, session) in _sessions)
        {
            yield return (key, session);
        }
    }
}
