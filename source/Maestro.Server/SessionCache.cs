using Maestro.Core.Sessions;

namespace Maestro.Server;

public class SessionCache
{
    readonly IDictionary<string, SessionMessage> _sessions = new Dictionary<string, SessionMessage>();

    public SessionMessage? Get(
        string partition,
        string airportIdentifier)
    {
        var key = CreateKey(partition, airportIdentifier);
        return !_sessions.TryGetValue(key, out var sessionMessage) ? null : sessionMessage;
    }

    public void Set(string partition, string airportIdentifier, SessionMessage sessionMessage)
    {
        var key = CreateKey(partition, airportIdentifier);
        _sessions[key] = sessionMessage;
    }

    // BUG: Need to remove old entries after the last client disconnects
    public void Evict(string partition, string airportIdentifier)
    {
        var key = CreateKey(partition, airportIdentifier);
        _sessions.Remove(key);
    }

    string CreateKey(string partition, string airportIdentifier)
    {
        return $"{partition}:{airportIdentifier}";
    }
}
