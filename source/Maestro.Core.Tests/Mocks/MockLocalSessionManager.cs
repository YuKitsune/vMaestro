using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;

namespace Maestro.Core.Tests.Mocks;

public class MockLocalSessionManager(Sequence sequence) : ISessionManager
{
    public string[] ActiveSessions => [sequence.AirportIdentifier];

    public Task CreateSession(string airportIdentifier, CancellationToken cancellationToken) => throw new NotImplementedException();

    public bool HasSessionFor(string airportIdentifier) => sequence.AirportIdentifier == airportIdentifier;

    public Task<IExclusiveSession> AcquireSession(string airportIdentifier, CancellationToken cancellationToken)
    {
        if (sequence.AirportIdentifier != airportIdentifier)
            throw new InvalidOperationException($"Cannot sequence for airport: {airportIdentifier}");

        return Task.FromResult<IExclusiveSession>(new MockExclusiveSession(new MockSession(sequence)));
    }

    public Task DestroySession(string airportIdentifier, CancellationToken cancellationToken) => throw new NotImplementedException();
}

public class MockExclusiveSession(ISession sequence) : IExclusiveSession
{
    public ISession Session => sequence;
    public void Dispose()
    {
        // No-op
    }
}

public class MockSession(Sequence sequence) : ISession
{
    public string AirportIdentifier => sequence.AirportIdentifier;
    public Sequence Sequence => sequence;
    public string Position => "MockPosition";
    public MaestroConnection? Connection => null;
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public ConnectionInfo? ConnectionInfo => null;
    public bool OwnsSequence => true;
    public Role Role => Role.Flow;
    public bool IsActive => true;
    public bool IsConnected => false;
    public Task Start(string position, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task Stop(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SetConnectionInfo(ConnectionInfo connectionInfo, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task Disconnect(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task TakeOwnership(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task RevokeOwnership(CancellationToken cancellationToken) => throw new NotImplementedException();
}
