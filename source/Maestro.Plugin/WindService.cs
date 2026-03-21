using Maestro.Contracts.Sessions;
using Maestro.Core.Hosting;
using Maestro.Plugin.Handlers;
using MediatR;
using Serilog;

namespace Maestro.Plugin;

public class WindService(IMaestroInstanceManager instanceManager, IMediator mediator, ILogger logger)
    : IAsyncDisposable
{
    // TODO: TimeProvider

    readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);
    readonly TimeSpan _errorInterval = TimeSpan.FromMinutes(5);
    readonly TimeSpan _windCheckTimeout = TimeSpan.FromMinutes(1);

    CancellationTokenSource? _cancellationTokenSource;
    Task? _task;

    public Task Start()
    {
        if (_task is not null || _cancellationTokenSource is not null)
            throw new InvalidOperationException("Wind check service already started");

        _cancellationTokenSource = new CancellationTokenSource();
        _task = DoWork(_cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    public async Task Stop()
    {
        if (_cancellationTokenSource is null || _task is null)
            throw new InvalidOperationException("Wind check service already stopped");

        _cancellationTokenSource.Cancel();
        await _task;

        _cancellationTokenSource.Dispose();
        _task.Dispose();

        _task = null;
        _cancellationTokenSource = null;
    }

    async Task DoWork(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    {
                        var timeoutCancellationTokenSource = new CancellationTokenSource(_windCheckTimeout);
                        foreach (var airportIdentifier in instanceManager.ActiveInstances)
                        {
                            await mediator.Send(
                                new RefreshWindRequest(airportIdentifier),
                                timeoutCancellationTokenSource.Token);
                        }
                    }

                    await Task.Delay(_checkInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Ignored
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error updating wind");
                    await Task.Delay(_errorInterval, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.Information("Wind check service stopping");
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Wind check service failed.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Stop();
    }
}
