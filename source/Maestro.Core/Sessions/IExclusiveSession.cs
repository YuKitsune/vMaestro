namespace Maestro.Core.Sessions;

public interface IExclusiveSession : IDisposable
{
    ISession Session { get; }
}
