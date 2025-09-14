using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;

namespace Maestro.Core.Tests.Mocks;

public class MockLocalSessionManager(Sequence sequence) : ISessionManager
{
    public string[] ActiveSessions => [sequence.AirportIdentifier];
    public Task CreateRemoteSession(string airportIdentifier, string server, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task CreateLocalSession(string airportIdentifier, CancellationToken cancellationToken) => throw new NotImplementedException();

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
    public MaestroConnection? Connection => null;
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public bool OwnsSequence => true;
    public Role Role => Role.Flow;
    public IReadOnlyDictionary<string, Role[]> Permissions => PermissionHelper.FullAccess();
    public Task Start(string position, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task Stop(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task TakeOwnership(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task RevokeOwnership(CancellationToken cancellationToken) => throw new NotImplementedException();
    public void ChangePermissions(IReadOnlyDictionary<string, Role[]> permissions) => throw new NotImplementedException();
}
