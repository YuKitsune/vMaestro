using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;

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
    IArrivalLookup arrivalLookup,
    IClock clock)
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
        if (!_instances.TryGetValue(airportIdentifier, out var session))
            throw new MaestroException($"No session exists for {airportIdentifier}.");

        await session.Semaphore.WaitAsync(cancellationToken);

        session.Dispose();
        _instances.Remove(airportIdentifier);
    }

    // TODO: Maybe extract into a factory
    MaestroInstance CreateInstance(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .FirstOrDefault(a => a.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            throw new MaestroException($"No configuration found for {airportIdentifier}");

        var session = new Session(airportConfiguration, arrivalLookup, clock);

        return new MaestroInstance(airportIdentifier, session);
    }
}
