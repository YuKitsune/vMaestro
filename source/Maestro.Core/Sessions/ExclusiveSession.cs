namespace Maestro.Core.Sessions;

public class ExclusiveSession(ISession session, SemaphoreSlim semaphoreSlim) : IExclusiveSession
{
    public ISession Session { get; } = session;

    public void Dispose()
    {
        semaphoreSlim.Release();
    }
}
