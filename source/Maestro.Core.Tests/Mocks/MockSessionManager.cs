using Maestro.Core.Sessions;

namespace Maestro.Core.Tests.Mocks;

public class MockSessionManager(Session session) : ISessionManager
{
    public string[] ActiveSessions => [session.AirportIdentifier];

    public Task CreateSession(string airportIdentifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public bool SessionExists(string airportIdentifier)
    {
        return session.AirportIdentifier == airportIdentifier;
    }

    public Task<Session> GetSession(string airportIdentifier, CancellationToken cancellationToken)
    {
        if (session.Sequence.AirportIdentifier != airportIdentifier)
            throw new InvalidOperationException($"Cannot sequence for airport: {airportIdentifier}");

        return Task.FromResult(session);
    }

    public Task DestroySession(string airportIdentifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

