using System.Threading.Channels;

namespace Maestro.Plugin;

public class WorkQueue : IAsyncDisposable
{
    readonly Action<Exception> _onError;
    readonly Channel<Func<Task>> _workQueue = Channel.CreateUnbounded<Func<Task>>();

    readonly CancellationTokenSource _cancellationTokenSource;
    readonly Task _worker;

    public WorkQueue(Action<Exception> onError)
    {
        _onError = onError;

        _cancellationTokenSource = new CancellationTokenSource();
        _worker = Worker(_cancellationTokenSource.Token);
    }

    public bool Enqueue(Func<Task> work)
    {
        return _workQueue.Writer.TryWrite(work);
    }

    async Task Worker(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var work = await _workQueue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                await work().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                try
                {
                    _onError(ex);
                }
                catch
                {
                    // Ignore errors during error reporting
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        await _worker.ConfigureAwait(false);

        _worker.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
