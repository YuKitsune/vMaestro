using System.Collections.Concurrent;
using Maestro.Contracts.Sessions;

namespace Maestro.Server;

/// <summary>
/// Identifies a unique session by environment and airport.
/// </summary>
/// <param name="Environment">The network environment (e.g., VATSIM).</param>
/// <param name="AirportIdentifier">The ICAO airport code (e.g., YSSY).</param>
public record SessionKey(string Environment, string AirportIdentifier);

public class SessionCache
{
    readonly ConcurrentDictionary<SessionKey, SessionDto> _sessions = new();

    public SessionDto? Get(string environment, string airportIdentifier)
    {
        var key = new SessionKey(environment, airportIdentifier);
        return _sessions.GetValueOrDefault(key);
    }

    public void Set(string environment, string airportIdentifier, SessionDto sessionDto)
    {
        var key = new SessionKey(environment, airportIdentifier);
        _sessions[key] = sessionDto;
    }

    public void Evict(string environment, string airportIdentifier)
    {
        var key = new SessionKey(environment, airportIdentifier);
        _sessions.TryRemove(key, out _);
    }

    public IEnumerable<(SessionKey Key, SessionDto Session)> GetAll()
    {
        return _sessions.Select(kvp => (kvp.Key, kvp.Value)).ToArray();
    }
}
