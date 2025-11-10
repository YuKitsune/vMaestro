using Maestro.Core.Model;

namespace Maestro.Core.Sessions;

public interface ISession : IDisposable
{
    string AirportIdentifier { get; }
    Sequence Sequence { get; }
    string Position { get; }
    SemaphoreSlim Semaphore { get; }
    bool IsActive { get; }

    Task Start(string position, CancellationToken cancellationToken);
    Task Stop(CancellationToken cancellationToken);
}

public class Session : ISession, IDisposable
{
    public string AirportIdentifier => Sequence.AirportIdentifier;
    public Sequence Sequence { get; private set; }
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public string Position { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public Session(Sequence sequence)
    {
        Sequence = sequence;
    }

    public Task Start(string position, CancellationToken cancellationToken)
    {
        Position = position;
        IsActive = true;
        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken)
    {
        Position = string.Empty;
        IsActive = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Semaphore.Dispose();
    }
}
