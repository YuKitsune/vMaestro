namespace Maestro.Core.Connectivity;

public interface IMaestroConnectionManager
{
    Uri CurrentServerUrl { get; }

    Task<IMaestroConnection> CreateConnection(
        string airportIdentifier,
        Uri serverUrl,
        string environment,
        CancellationToken cancellationToken);

    bool TryGetConnection(string airportIdentifier, out IMaestroConnection? connection);

    Task RemoveConnection(string airportIdentifier, CancellationToken cancellationToken);
}
