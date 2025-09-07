using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Maestro.Core.Infrastructure;

public class NotificationStream<T> : INotificationStream<T>, IDisposable
{
    private readonly ChannelWriter<T> _writer;
    private readonly ChannelReader<T> _reader;
    private bool _disposed;

    public NotificationStream(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        var channel = Channel.CreateBounded<T>(options);
        _writer = channel.Writer;
        _reader = channel.Reader;
    }

    public async Task PublishAsync(T notification, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_writer.TryWrite(notification))
            return;

        await _writer.WriteAsync(notification, cancellationToken);
    }

    public async IAsyncEnumerable<T> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await foreach (var notification in _reader.ReadAllAsync(cancellationToken))
        {
            yield return notification;
        }
    }

    private void ThrowIfDisposed()
    {
        if (!_disposed)
            return;

        throw new ObjectDisposedException(nameof(NotificationStream<T>));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _writer.TryComplete();
        _disposed = true;
    }
}
