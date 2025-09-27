using Maestro.Core.Messages;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public class SequenceUpdatedNotificationHandler(IConnectionManager connectionManager, SequenceCache sequenceCache, IHubProxy hubProxy, ILogger logger)
    : INotificationHandler<NotificationContextWrapper<SequenceUpdatedNotification>>
{
    public async Task Handle(NotificationContextWrapper<SequenceUpdatedNotification> wrappedNotification, CancellationToken cancellationToken)
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

        logger.Information("{Connection} updating sequence", connection);

        sequenceCache.Set(connection.Partition, notification.AirportIdentifier, notification.Sequence);

        // Relay to all other clients in the sequence
        var peers = connectionManager.GetPeers(connection);
        foreach (var peer in peers)
        {
            await hubProxy.Send(peer.Id, "SequenceUpdated", notification, cancellationToken);
        }
    }
}
