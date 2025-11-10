using Maestro.Core.Connectivity.Contracts;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public record ClientDisconnectedNotification(string ConnectionId) : INotification;

// TODO: Test cases
// - When connection is untracked, exception is thrown
// - When leaving the sequence, peers are notified
// - When master leaves the sequence, and another flow controller exists, the flow controller is promoted
// - When master leaves the sequence, and no flow controller exists, the next available connection is promoted

public class ClientDisconnectedNotificationHandler(
    IConnectionManager connectionManager,
    SequenceCache sequenceCache,
    IHubProxy hubProxy,
    ILogger logger)
    : INotificationHandler<ClientDisconnectedNotification>
{
    public async Task Handle(ClientDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.ConnectionId, out var connection))
        {
            throw new InvalidOperationException($"Connection {notification.ConnectionId} is not tracked");
        }

        logger.Information("{Connection} untracked", connection);
        connectionManager.Remove(connection);

        var remainingPeers = connectionManager.GetPeers(connection);

        if (connection.IsMaster)
        {
            var eligiblePeers = remainingPeers
                .Where(c => c.Role is not Role.Observer)
                .ToArray();

            // Master has left, need to re-assign to someone else
            // Prefer another flow controller, otherwise the next eligible connection
            var newMaster = eligiblePeers.FirstOrDefault(c => c.Role == Role.Flow) ?? eligiblePeers.FirstOrDefault();
            if (newMaster is not null)
            {
                newMaster.IsMaster = true;

                await hubProxy.Send(
                    newMaster.Id,
                    "OwnershipGranted",
                    new OwnershipGrantedNotification(connection.AirportIdentifier),
                    cancellationToken);

                logger.Information("Promoting {Connection} to master", newMaster);
            }
        }

        if (remainingPeers.Length == 0 || remainingPeers.All(p => p.Role == Role.Observer))
        {
            sequenceCache.Evict(connection.Partition, connection.AirportIdentifier);
        }

        // Broadcast to remaining clients that this client has disconnected
        foreach (var remainingConnection in remainingPeers)
        {
            await hubProxy.Send(
                remainingConnection.Id,
                "PeerDisconnected",
                new PeerDisconnectedNotification(connection.AirportIdentifier, connection.Callsign),
                cancellationToken);
        }
    }
}
