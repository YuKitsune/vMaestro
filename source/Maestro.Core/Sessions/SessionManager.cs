using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions;

public class SessionManager(
    IAirportConfigurationProvider airportConfigurationProvider,
    IArrivalLookup arrivalLookup,
    IClock clock)
    : ISessionManager
{
    readonly SemaphoreSlim _semaphore = new(1, 1);
    readonly IDictionary<string, ISession> _sessions = new Dictionary<string, ISession>();

    public string[] ActiveSessions => _sessions.Keys.ToArray();

    public async Task CreateSession(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (_sessions.ContainsKey(airportIdentifier))
            throw new MaestroException($"Session already exists for {airportIdentifier}.");

        var sequence = CreateSequence(airportIdentifier);
        _sessions[airportIdentifier] = new Session(sequence);
    }

    public bool HasSessionFor(string airportIdentifier) => _sessions.ContainsKey(airportIdentifier);

    public async Task<IExclusiveSession> AcquireSession(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (!_sessions.TryGetValue(airportIdentifier, out var session))
            throw new MaestroException($"No session exists for {airportIdentifier}.");

        await session.Semaphore.WaitAsync(cancellationToken);
        return new ExclusiveSession(session, session.Semaphore);
    }

    public async Task DestroySession(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (!_sessions.TryGetValue(airportIdentifier, out var session))
            throw new MaestroException($"No session exists for {airportIdentifier}.");

        session.Dispose();
        _sessions.Remove(airportIdentifier);
    }

    // TODO: Maybe extract into a factory
    Sequence CreateSequence(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .FirstOrDefault(a => a.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            throw new MaestroException($"No configuration found for {airportIdentifier}");

        return new Sequence(airportConfiguration, arrivalLookup, clock);
    }
}
