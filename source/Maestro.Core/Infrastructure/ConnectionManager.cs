using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Maestro.Core.Infrastructure;

// TODO:
// - [ ] Dispatch messages to both Mediator (for in-memory stuff) and SignalR (when connected); Disregard permissions for now
// - [ ] Connect to Maestro server when starting the sequence
// - [ ] Test with two clients connected at once, what breaks?
// - [ ] Restrict access to certain functions from certain clients
// - [ ] Show information messages when things happen
// - [ ] Coordination messages

public record SubscribeRequest(string AirportIdentifier, string Position);

public static class MethodNames
{
    public const string Subscribe = "Subscribe";
}

public class MaestroConnection(
    string airportIdentifier,
    string position,
    HubConnection hubConnection,
    IMediator mediator)
    : IAsyncDisposable
{
    readonly CancellationTokenSource _rootCancellationTokenSource = new();

    public async Task Start(CancellationToken cancellationToken)
    {
        await hubConnection.StartAsync(cancellationToken);
        await hubConnection.SendAsync(MethodNames.Subscribe, new SubscribeRequest(airportIdentifier, position), cancellationToken);
    }

    public async Task Send<T>(T message, CancellationToken cancellationToken)
    {
        // TODO: Derive method name from message type
        await hubConnection.SendAsync(message.GetType().Name, message, cancellationToken);
    }

    void SubscribeToNotifications()
    {
        RelayToMediator<SequenceUpdatedNotification>(nameof(SequenceUpdatedNotification));
        RelayToMediator<FlightUpdatedNotification>(nameof(SequenceUpdatedNotification));
    }

    void RelayToMediator<T>(string methodName) where T : INotification
    {
        hubConnection.On<T>(methodName, async message =>
        {
            await mediator.Publish(message, GetMessageCancellationToken());
        });
    }

    CancellationToken GetMessageCancellationToken()
    {
        var source = new CancellationTokenSource();
        source.CancelAfter(TimeSpan.FromSeconds(5));

        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(_rootCancellationTokenSource.Token, source.Token);
        return linkedSource.Token;
    }

    public async ValueTask DisposeAsync()
    {
        await hubConnection.DisposeAsync();
        _rootCancellationTokenSource.Cancel();
        _rootCancellationTokenSource.Dispose();
    }
}

public class ServerConfiguration
{
    public required Uri Url { get; init; }
}

public class MaestroConnectionManager(IMediator mediator, ServerConfiguration configuration) : IAsyncDisposable
{
    readonly IDictionary<string, MaestroConnection> _connections = new Dictionary<string, MaestroConnection>();

    public async Task<MaestroConnection> GetOrCreateConnection(string airportIdentifier, string position, CancellationToken cancellationToken)
    {
        if (_connections.TryGetValue(airportIdentifier, out var connection))
            return connection;

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(configuration.Url)
            .Build();

        connection = new MaestroConnection(airportIdentifier, position, hubConnection, mediator);
        await connection.Start(cancellationToken);
        _connections.Add(airportIdentifier, connection);
        return connection;
    }

    public bool TryGetConnection(string airportIdentifier, out MaestroConnection? connection)
    {
        return _connections.TryGetValue(airportIdentifier, out connection);
    }

    public async Task<bool> TryRemoveConnection(string airportIdentifier)
    {
        if (!_connections.TryGetValue(airportIdentifier, out var connection))
            return false;

        await connection.DisposeAsync();
        _connections.Remove(airportIdentifier);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
        {
            await connection.DisposeAsync();
        }
    }
}
