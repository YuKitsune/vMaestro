namespace Maestro.Core.Sessions;

public interface ISessionManager
{
    string[] ActiveSessions { get; }
    Task CreateSession(string airportIdentifier, CancellationToken cancellationToken);
    bool HasSessionFor(string airportIdentifier);
    Task<IExclusiveSession> AcquireSession(string airportIdentifier, CancellationToken cancellationToken);
    Task DestroySession(string airportIdentifier, CancellationToken cancellationToken);
}
