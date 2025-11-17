namespace Maestro.Core.Connectivity;

public interface IMaestroConnectionManager
{
    Task<IMaestroConnection> CreateConnection(
        string airportIdentifier,
        string partition,
        CancellationToken cancellationToken);

    bool TryGetConnection(string airportIdentifier, out IMaestroConnection? connection);

    Task RemoveConnection(string airportIdentifier, CancellationToken cancellationToken);
}
