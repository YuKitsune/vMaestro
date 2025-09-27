using Maestro.Core.Messages;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

// TODO: Test cases
// - When the client is not tracked, exception is thrown
// - When the airport identifiers mismatch, exception is thrown
// - When the client is not the master, exception is thrown
// - When the sequence is updated, the cache is updated, and all other clients are notified

public class SequenceUpdatedNotificationHandler(IConnectionManager connectionManager, SequenceCache sequenceCache, IHubContext<MaestroHub> hubContext, ILogger logger)
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
            await hubContext.Clients
                .Client(peer.Id)
                .SendAsync("SequenceUpdated", notification, cancellationToken);
        }
    }
}
