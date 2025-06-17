namespace Maestro.Core.Extensions;

public static class SemaphoreSlimExtensionMethods
{
    public static IDisposable Lock(this SemaphoreSlim semaphoreSlim)
    {
        semaphoreSlim.Wait();
        return new Releaser(semaphoreSlim);
    }
    public static async Task<IDisposable> LockAsync(this SemaphoreSlim semaphoreSlim, CancellationToken cancellationToken = default)
    {
        await semaphoreSlim.WaitAsync(cancellationToken);
        return new Releaser(semaphoreSlim);
    }

    class Releaser(SemaphoreSlim semaphoreSlim) : IDisposable
    {
        public void Dispose() => semaphoreSlim.Release();
    }
}