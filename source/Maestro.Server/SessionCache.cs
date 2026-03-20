using Maestro.Contracts.Sessions;

namespace Maestro.Server;

/// <summary>
/// Identifies a unique session by partition and airport.
/// </summary>
/// <param name="Partition">The network partition (e.g., VATSIM).</param>
/// <param name="AirportIdentifier">The ICAO airport code (e.g., YSSY).</param>
public record SessionKey(string Partition, string AirportIdentifier);

public class SessionCache
{
    readonly Dictionary<SessionKey, SessionDto> _sessions = new();

    public SessionDto? Get(string partition, string airportIdentifier)
    {
        var key = new SessionKey(partition, airportIdentifier);
        return _sessions.GetValueOrDefault(key);
    }

    public void Set(string partition, string airportIdentifier, SessionDto sessionDto)
    {
        var key = new SessionKey(partition, airportIdentifier);
        _sessions[key] = sessionDto;
    }

    // BUG: Need to remove old entries after the last client disconnects
    public void Evict(string partition, string airportIdentifier)
    {
        var key = new SessionKey(partition, airportIdentifier);
        _sessions.Remove(key);
    }

    public IEnumerable<(SessionKey Key, SessionDto Session)> GetAll()
    {
        foreach (var (key, session) in _sessions)
        {
            yield return (key, session);
        }
    }
}
