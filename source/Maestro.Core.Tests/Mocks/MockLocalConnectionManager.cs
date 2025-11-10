using Maestro.Core.Connectivity;

namespace Maestro.Core.Tests.Mocks;

public class MockLocalConnectionManager : IMaestroConnectionManager
{
    public Task<MaestroConnection> CreateConnection(string airportIdentifier, string partition, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public bool TryGetConnection(string airportIdentifier, out MaestroConnection? connection)
    {
        connection = null;
        return false;
    }

    public Task RemoveConnection(string airportIdentifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
