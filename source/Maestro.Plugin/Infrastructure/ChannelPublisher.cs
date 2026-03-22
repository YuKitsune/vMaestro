using System.Threading.Channels;
using MediatR;
using Serilog;
using vatsys;

namespace Maestro.Plugin.Infrastructure;

public class AsyncNotificationPublisher : INotificationPublisher, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Channel<NotificationWorkItem> _channel;
    private readonly Task _processTask;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public AsyncNotificationPublisher(ILogger logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<NotificationWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _cancellationTokenSource = new CancellationTokenSource();
        _processTask = ProcessNotificationsAsync(_cancellationTokenSource.Token);
    }

    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        // Queue the work item and return immediately
        await _channel.Writer.WriteAsync(
            new NotificationWorkItem(handlerExecutors.ToArray(), notification),
            cancellationToken);
    }

    private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
    {
        await foreach (var workItem in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                // Process handlers in parallel so slow handlers don't block others
                var tasks = workItem.Handlers.Select(async handler =>
                {
                    try
                    {
                        await handler.HandlerCallback(workItem.Notification, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore cancellation for individual handlers
                    }
                    catch (Exception exception)
                    {
                        // Log error but don't stop processing other handlers
                        _logger.Error(exception, "Failed to process notification {NotificationType} in handler {HandlerType}",
                            workItem.Notification.GetType(), handler.GetType());
                        Errors.Add(exception, Plugin.Name);
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception exception)
            {
                // This should rarely happen since individual handler errors are caught above
                _logger.Error(exception, "Failed to process notification {NotificationType}", workItem.Notification.GetType());
                Errors.Add(exception, Plugin.Name);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        _channel.Writer.Complete();
        await _processTask;
        _cancellationTokenSource.Dispose();
    }

    private record NotificationWorkItem(
        NotificationHandlerExecutor[] Handlers,
        INotification Notification);
}
