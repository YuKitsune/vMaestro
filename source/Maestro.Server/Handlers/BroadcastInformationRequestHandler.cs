using Maestro.Core.Handlers;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

// TODO: Test cases
// - When the connection is not tracked, exception is thrown
// - When the connection is the mater, exception is thrown
// - Information messages are broadcast to all peers

public class BroadcastInformationRequestHandler(IConnectionManager connectionManager, IHubContext<MaestroHub> hubContext, ILogger logger)
    : INotificationHandler<NotificationContextWrapper<InformationNotification>>
{
    public async Task Handle(NotificationContextWrapper<InformationNotification> wrappedNotification, CancellationToken cancellationToken)
    {
        var (connectionId, notification) = wrappedNotification;

        if (!connectionManager.TryGetConnection(connectionId, out var connection))
        {
            throw new InvalidOperationException($"Connection {connectionId} is not tracked");
        }

        if (!connection.IsMaster)
        {
            throw new InvalidOperationException("Only the master can broadcast information");
        }

        var peers = connectionManager.GetPeers(connection);
        logger.Information("{Connection} broadcasting information to peers", connection);

        foreach (var peer in peers)
        {
            await hubContext.Clients
                .Client(peer.Id)
                .SendAsync("Information", notification, cancellationToken);
        }
    }
}
