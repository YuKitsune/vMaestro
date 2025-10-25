using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public class CoordinationNotificationHandler(IConnectionManager connectionManager, IHubProxy hubProxy, ILogger logger)
    : INotificationHandler<NotificationContextWrapper<CoordinationNotification>>
{
    public async Task Handle(NotificationContextWrapper<CoordinationNotification> wrappedNotification, CancellationToken cancellationToken)
    {
        var (connectionId, notification) = wrappedNotification;

        if (!connectionManager.TryGetConnection(connectionId, out var connection))
        {
            throw new InvalidOperationException($"Connection {connectionId} is not tracked");
        }

        var peers = connectionManager.GetPeers(connection);

        switch (notification.Destination)
        {
            case CoordinationDestination.Broadcast:
                logger.Information("{Connection} broadcasting coordination message to all peers: {Message}",
                    connection, notification.Message);

                foreach (var peer in peers)
                {
                    await hubProxy.Send(peer.Id, "Coordination", notification, cancellationToken);
                }

                break;

            case CoordinationDestination.Controller controllerDest:
                // Send to specific controller
                var targetPeer = peers.FirstOrDefault(p => p.Callsign == controllerDest.Callsign);
                if (targetPeer is not null)
                {
                    logger.Information("{Connection} sending coordination message to {Target}: {Message}",
                        connection, targetPeer, notification.Message);

                    await hubProxy.Send(targetPeer.Id, "Coordination", notification, cancellationToken);
                }
                else
                {
                    logger.Warning("{Connection} attempted to send coordination to unknown controller {Target}",
                        connection, controllerDest.Callsign);
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown destination type: {notification.Destination.GetType().Name}");
        }
    }
}
