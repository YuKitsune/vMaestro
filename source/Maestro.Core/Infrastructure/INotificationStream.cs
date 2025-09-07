namespace Maestro.Core.Infrastructure;

public interface INotificationStream<T>
{
    Task PublishAsync(T notification, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> SubscribeAsync(CancellationToken cancellationToken = default);
}
