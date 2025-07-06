using System.Diagnostics;
using System.Threading.Channels;
using MediatR;
using Serilog;

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
        _logger.Debug("{NotificationType} published",
            notification.GetType());
    }

    private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
    {
        await foreach (var workItem in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                foreach (var notificationHandlerExecutor in workItem.Handlers)
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    await notificationHandlerExecutor.HandlerCallback(workItem.Notification, cancellationToken);
                    _logger.Debug("{HandlerType} handled {NotificationType} in {ElapsedMilliseconds}ms",
                        notificationHandlerExecutor.HandlerInstance.GetType(),
                        workItem.Notification.GetType(),
                        sw.Elapsed);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception exception)
            {
                // Log error but continue processing other notifications
                _logger.Error(exception, "Failed to process notification {NotificationType}", workItem.Notification.GetType());
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