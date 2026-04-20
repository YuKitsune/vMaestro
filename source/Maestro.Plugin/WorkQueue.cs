using System.Threading.Channels;
using Maestro.Wpf.Integrations;

namespace Maestro.Plugin;

public class WorkQueue : IAsyncDisposable
{
    readonly IErrorReporter _errorReporter;
    readonly Channel<Func<Task>> _workQueue = Channel.CreateUnbounded<Func<Task>>();

    readonly CancellationTokenSource _cancellationTokenSource;
    readonly Task _worker;

    public WorkQueue(IErrorReporter errorReporter)
    {
        _errorReporter = errorReporter;

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
                    _errorReporter.ReportError(ex);
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
