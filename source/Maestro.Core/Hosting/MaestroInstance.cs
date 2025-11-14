using Maestro.Core.Sessions;

namespace Maestro.Core.Hosting;

public class MaestroInstance(string airportIdentifier, Session session) : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    public string AirportIdentifier { get; } = airportIdentifier;
    public Session Session { get; } = session;

    public void Dispose()
    {
        Semaphore.Dispose();
    }
}
