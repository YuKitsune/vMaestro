namespace Maestro.Core.Extensions;

public static class SemaphoreSlimExtensionMethods
{
    public static IDisposable Lock(this SemaphoreSlim semaphoreSlim)
    {
        semaphoreSlim.Wait();
        return new Disposable(() => semaphoreSlim.Release());
    }

    class Disposable(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}