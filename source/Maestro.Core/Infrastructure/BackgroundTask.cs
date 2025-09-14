namespace Maestro.Core.Infrastructure;

public class BackgroundTask(Func<CancellationToken, Task> worker) : IAsyncDisposable
{
    bool IsDisposed { get; set; }
    readonly Func<CancellationToken, Task> _worker = worker;

    CancellationTokenSource? _stoppingTokenSource;
    Task? _executingTask;

    public bool IsRunning => _executingTask is { IsCompleted: false };

    public async Task Start(CancellationToken _)
    {
        await Task.CompletedTask;

        if (IsDisposed)
            throw new ObjectDisposedException(nameof(BackgroundTask));

        _stoppingTokenSource = new CancellationTokenSource();
        _executingTask = _worker(_stoppingTokenSource.Token);
    }

    public async Task Stop(CancellationToken _)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(BackgroundTask));

        if (_executingTask == null || _stoppingTokenSource == null)
            return;

        _stoppingTokenSource.Cancel();

        await _executingTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
            return;

        if (_executingTask is not null)
        {
            await _executingTask;
            _executingTask?.Dispose();
        }

        _stoppingTokenSource?.Dispose();
        IsDisposed = true;
    }
}
