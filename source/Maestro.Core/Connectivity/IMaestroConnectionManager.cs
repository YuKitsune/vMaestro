namespace Maestro.Core.Connectivity;

public interface IMaestroConnectionManager
{
    Task<MaestroConnection> CreateConnection(
        string airportIdentifier,
        string partition,
        CancellationToken cancellationToken);

    bool TryGetConnection(string airportIdentifier, out MaestroConnection? connection);

    Task RemoveConnection(string airportIdentifier, CancellationToken cancellationToken);
}
