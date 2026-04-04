using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Hosting;

public interface IMaestroInstanceManager
{
    string[] ActiveInstances { get; }
    Task CreateInstance(string airportIdentifier, CancellationToken cancellationToken);
    bool InstanceExists(string airportIdentifier);
    Task<MaestroInstance> GetInstance(string airportIdentifier, CancellationToken cancellationToken);
    Task DestroyInstance(string airportIdentifier, CancellationToken cancellationToken);
}

public class MaestroInstanceManager(
    IAirportConfigurationProvider airportConfigurationProvider,
    ITrajectoryService trajectoryService,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IMaestroInstanceManager
{
    readonly SemaphoreSlim _semaphore = new(1, 1);
    readonly IDictionary<string, MaestroInstance> _instances = new Dictionary<string, MaestroInstance>();

    public string[] ActiveInstances => _instances.Keys.ToArray();

    public async Task CreateInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (_instances.ContainsKey(airportIdentifier))
            throw new MaestroException($"Instance already exists for {airportIdentifier}.");

        var instance = CreateInstance(airportIdentifier);
        _instances[airportIdentifier] = instance;
    }

    public bool InstanceExists(string airportIdentifier) => _instances.ContainsKey(airportIdentifier);

    public async Task<MaestroInstance> GetInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (!_instances.TryGetValue(airportIdentifier, out var instance))
            throw new MaestroException($"No instance exists for {airportIdentifier}.");

        return instance;
    }

    public async Task DestroyInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        if (!_instances.TryGetValue(airportIdentifier, out var instance))
            throw new MaestroException($"No instance exists for {airportIdentifier}.");

        await instance.Semaphore.WaitAsync(cancellationToken);

        await instance.DisposeAsync();
        _instances.Remove(airportIdentifier);
    }

    // TODO: Maybe extract into a factory
    MaestroInstance CreateInstance(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);
        if (airportConfiguration is null)
            throw new MaestroException($"No configuration found for {airportIdentifier}");

        var session = new Session(
            new Sequence(airportConfiguration, trajectoryService, clock, logger),
            logger);

        return new MaestroInstance(airportIdentifier, session, mediator);
    }
}
