using Maestro.Core.Hosting;
using Maestro.Core.Model;
using Maestro.Core.Sessions;

namespace Maestro.Core.Tests.Mocks;

public class MockInstanceManager(Sequence sequence) : IMaestroInstanceManager
{
    readonly Session _session = new(sequence);
    public string[] ActiveInstances => [sequence.AirportIdentifier];

    public Task CreateInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public bool InstanceExists(string airportIdentifier)
    {
        return _session.AirportIdentifier == airportIdentifier;
    }

    public Task<MaestroInstance> GetInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        if (sequence.AirportIdentifier != airportIdentifier)
            throw new InvalidOperationException($"Cannot sequence for airport: {airportIdentifier}");

        return Task.FromResult(new MaestroInstance(airportIdentifier, _session));
    }

    public Task DestroyInstance(string airportIdentifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

