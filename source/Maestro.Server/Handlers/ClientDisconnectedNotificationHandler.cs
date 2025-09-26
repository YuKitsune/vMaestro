using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Maestro.Server.Handlers;

public record ClientDisconnectedNotification(string ConnectionId) : INotification;

// TODO: Test cases
// - When connection is untracked, exception is thrown
// - When leaving the sequence, peers are notified
// - When master leaves the sequence, and another flow controller exists, the flow controller is promoted
// - When master leaves the sequence, and no flow controller exists, the next available connection is promoted

public class ClientDisconnectedNotificationHandler(IConnectionManager connectionManager, IHubContext hubContext, ILogger logger)
    : INotificationHandler<ClientDisconnectedNotification>
{
    public async Task Handle(ClientDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.ConnectionId, out var connection))
        {
            throw new InvalidOperationException($"Connection {notification.ConnectionId} is not tracked");
        }

        logger.LogInformation("{Connection} untracked", connection);
        connectionManager.Remove(connection);

        var remainingPeers = connectionManager.GetPeers(connection);

        if (connection.IsMaster)
        {
            var eligiblePeers = remainingPeers
                .Where(c => c.Role is not Role.Observer)
                .ToArray();

            // Master has left, need to re-assign to someone else
            // Prefer the flow controller, otherwise the next available connection
            var newMaster = eligiblePeers.FirstOrDefault(c => c.Role == Role.Flow) ?? eligiblePeers.First();
            newMaster.IsMaster = true;

            await hubContext.Clients
                .Client(newMaster.Id)
                .SendAsync(
                    "OwnershipGranted",
                    new OwnershipGrantedNotification(connection.AirportIdentifier),
                    cancellationToken);

            logger.LogInformation("Promoting {Connection} to master", newMaster);
        }

        // Broadcast to remaining clients that this client has disconnected
        foreach (var remainingConnection in remainingPeers)
        {
            await hubContext.Clients
                .Client(remainingConnection.Id)
                .SendAsync(
                    "PeerDisconnected",
                    new PeerDisconnectedNotification(connection.AirportIdentifier, connection.Callsign),
                    cancellationToken);
        }
    }
}
