using Maestro.Core.Messages;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public class SessionUpdatedNotificationHandler(IConnectionManager connectionManager, SessionCache sessionCache, IHubProxy hubProxy, ILogger logger)
    : INotificationHandler<NotificationContextWrapper<SessionUpdatedNotification>>
{
    public async Task Handle(NotificationContextWrapper<SessionUpdatedNotification> wrappedNotification, CancellationToken cancellationToken)
    {
        var (connectionId, notification) = wrappedNotification;
        if (!connectionManager.TryGetConnection(connectionId, out var connection))
        {
            throw new InvalidOperationException($"Connection {connectionId} is not tracked");
        }

        if (notification.AirportIdentifier != connection.AirportIdentifier)
        {
            throw new InvalidOperationException($"Connection {connectionId} attempted to update {notification.AirportIdentifier} but is connected to {connection.AirportIdentifier}");
        }

        if (!connection.IsMaster)
        {
            throw new InvalidOperationException("Only the master can update the sequence");
        }

        logger.Information("{Connection} updating session", connection);

        sessionCache.Set(connection.Partition, notification.AirportIdentifier, notification.Session);

        // Relay to all other clients in the session
        var peers = connectionManager.GetPeers(connection);
        foreach (var peer in peers)
        {
            await hubProxy.Send(peer.Id, "SessionUpdated", notification, cancellationToken);
        }
    }
}
