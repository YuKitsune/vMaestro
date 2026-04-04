using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions;

public interface ISessionManager
{
    string[] ActiveSessions { get; }
    Task CreateSession(string airportIdentifier, CancellationToken cancellationToken);
    bool SessionExists(string airportIdentifier);
    Task<Session> GetSession(string airportIdentifier, CancellationToken cancellationToken);
    Task DestroySession(string airportIdentifier, CancellationToken cancellationToken);
}

public class SessionManager(
    IAirportConfigurationProvider airportConfigurationProvider,
    ITrajectoryService trajectoryService,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : ISessionManager
{
    readonly SemaphoreSlim _semaphore = new(1, 1);
    readonly IDictionary<string, Session> _sessions = new Dictionary<string, Session>();

    public string[] ActiveSessions => _sessions.Keys.ToArray();

    public async Task CreateSession(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (_sessions.ContainsKey(airportIdentifier))
            throw new MaestroException($"Session already exists for {airportIdentifier}.");

        var session = CreateSession(airportIdentifier);
        _sessions[airportIdentifier] = session;
    }

    public bool SessionExists(string airportIdentifier) => _sessions.ContainsKey(airportIdentifier);

    public async Task<Session> GetSession(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (!_sessions.TryGetValue(airportIdentifier, out var session))
            throw new MaestroException($"No session exists for {airportIdentifier}.");

        return session;
    }

    public async Task DestroySession(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (!_sessions.TryGetValue(airportIdentifier, out var session))
            throw new MaestroException($"No session exists for {airportIdentifier}.");

        await session.DisposeAsync();
        _sessions.Remove(airportIdentifier);
    }

    Session CreateSession(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);
        if (airportConfiguration is null)
            throw new MaestroException($"No configuration found for {airportIdentifier}");

        var sequence = new Sequence(airportConfiguration, trajectoryService, clock, logger);
        return new Session(sequence, mediator, logger);
    }
}
