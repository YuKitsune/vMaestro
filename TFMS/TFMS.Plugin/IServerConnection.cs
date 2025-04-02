using System.Windows.Media;

namespace TFMS.Plugin;

public interface IServerConnection
{
    bool IsConnected { get; }

    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken);
    Task<TResponse> SendAsync<TPayload, TResponse>(TPayload payload, CancellationToken cancellationToken);
}

public class StubServerConnection : IServerConnection
{
    public bool IsConnected => false;

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<TResponse> SendAsync<TPayload, TResponse>(TPayload payload, CancellationToken cancellationToken)
    {
        return Task.FromResult<TResponse>(default);
    }
}