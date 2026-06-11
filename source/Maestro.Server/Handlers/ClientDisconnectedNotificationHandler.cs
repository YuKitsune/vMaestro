using Maestro.Contracts.Connectivity;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public record ClientDisconnectedNotification(string ConnectionId) : INotification;

// TODO: Test cases
// - When connection is untracked, exception is thrown
// - When leaving the sequence, peers are notified
// - When master leaves the sequence, the highest-priority remaining connection is promoted
// - When master leaves the sequence, and only observers remain, no new master is promoted

public class ClientDisconnectedNotificationHandler(
    IConnectionManager connectionManager,
    SessionCache sessionCache,
    IHubProxy hubProxy,
    ILogger logger)
    : INotificationHandler<ClientDisconnectedNotification>
{
    public async Task Handle(ClientDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.ConnectionId, out var connection))
        {
            return; // Connection not tracked, nothing to do
        }

        logger.Information("{Connection} untracked", connection);
        connectionManager.Remove(connection);

        var remainingPeers = connectionManager.GetPeers(connection);

        if (connection.IsMaster)
        {
            var eligiblePeers = remainingPeers
                .Where(c => c.Role is not Role.Observer)
                .ToArray();

            var newMaster = eligiblePeers.OrderByDescending(c => GetMasterPriority(c.Role)).FirstOrDefault();
            if (newMaster is not null)
            {
                connectionManager.PromoteMaster(newMaster);

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
            sessionCache.Evict(connection.Environment, connection.AirportIdentifier);
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

    static int GetMasterPriority(Role role) => role switch
    {
        Role.Flow => 3,
        Role.Enroute => 2,
        Role.Approach => 1,
        _ => 0
    };
}
