using Maestro.Core.Hosting;
using Maestro.Core.Model;
using Maestro.Core.Sessions;

namespace Maestro.Core.Tests.Mocks;

public class MockInstanceManager(MaestroInstance instance) : IMaestroInstanceManager
{
    readonly MaestroInstance _instance = instance;

    public string[] ActiveInstances => [_instance.AirportIdentifier];

    public Task CreateInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public bool InstanceExists(string airportIdentifier)
    {
        return _instance.AirportIdentifier == airportIdentifier;
    }

    public Task<MaestroInstance> GetInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        if (_instance.Session.Sequence.AirportIdentifier != airportIdentifier)
            throw new InvalidOperationException($"Cannot sequence for airport: {airportIdentifier}");

        return Task.FromResult(new MaestroInstance(airportIdentifier, _instance.Session));
    }

    public Task DestroyInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

