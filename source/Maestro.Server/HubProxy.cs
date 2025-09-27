using Microsoft.AspNetCore.SignalR;

namespace Maestro.Server;

public interface IHubProxy
{
    Task Send<TMessage>(string clientId, string method, TMessage message, CancellationToken cancellationToken);
    Task<TResponse> Invoke<TRequest, TResponse>(string clientId, string method, TRequest request, CancellationToken cancellationToken);
}

public class HubProxy(IHubContext<MaestroHub> hubContext) : IHubProxy
{
    public Task Send<TMessage>(string clientId, string method, TMessage message, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Client(clientId).SendAsync(method, message, cancellationToken);
    }

    public Task<TResponse> Invoke<TRequest, TResponse>(string clientId, string method, TRequest request, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Client(clientId).InvokeAsync<TResponse>(method, request, cancellationToken);
    }
}
