using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Maestro.Core.Sessions;

public class SessionManager(
    IAirportConfigurationProvider airportConfigurationProvider,
    ServerConfiguration serverConfiguration,
    ISessionFactory sessionFactory,
    IMediator mediator,
    ILogger logger)
    : ISessionManager
{
    readonly SemaphoreSlim _semaphore = new(1, 1);
    readonly IDictionary<string, Session> _sessions = new Dictionary<string, Session>();

    public string[] ActiveSessions => _sessions.Keys.ToArray();

    public async Task CreateRemoteSession(string airportIdentifier, string partition, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (_sessions.ContainsKey(airportIdentifier))
            throw new MaestroException($"Session already exists for {airportIdentifier}.");

        var sequence = CreateSequence(airportIdentifier);
        var connection = CreateConnection(serverConfiguration, partition, airportIdentifier);
        _sessions[airportIdentifier] = sessionFactory.Create(sequence, connection);
    }

    public async Task CreateLocalSession(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (_sessions.ContainsKey(airportIdentifier))
            throw new MaestroException($"Session already exists for {airportIdentifier}.");

        var sequence = CreateSequence(airportIdentifier);
        _sessions[airportIdentifier] = sessionFactory.Create(sequence);
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

        await session.DisposeAsync();
        _sessions.Remove(airportIdentifier);
    }

    // TODO: Maybe extract into a factory
    MaestroConnection CreateConnection(ServerConfiguration configuration, string partition, string airportIdentifier)
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(configuration.Uri)
            .WithServerTimeout(TimeSpan.FromSeconds(configuration.TimeoutSeconds))
            .WithAutomaticReconnect()
            .WithStatefulReconnect()
            .AddNewtonsoftJsonProtocol()
            .Build();

        return new MaestroConnection(partition, airportIdentifier, configuration, hubConnection, mediator, logger);
    }

    // TODO: Maybe extract into a factory
    Sequence CreateSequence(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .FirstOrDefault(a => a.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            throw new MaestroException($"No configuration found for {airportIdentifier}");

        return new Sequence(airportConfiguration);
    }
}
