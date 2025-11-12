namespace Maestro.Core.Sessions;

public interface IExclusiveSession : IDisposable
{
    Session Session { get; }
}
