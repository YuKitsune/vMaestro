using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Maestro.Server.Handlers;

public record RequestContextWrapper<T>(string ConnectionId, T Request) : IRequest;

public record RequestContextWrapper<TRequest, TResponse>(string ConnectionId, TRequest Request) : IRequest<TResponse>;

public record NotificationContextWrapper<T>(string ConnectionId, T Notification) : INotification;

// TODO: Test Cases
// - When no peers exists, the first connection becomes the master
// - When no peers exist, and the first connection is an observer, the observer does not become the master
// - When peers exist, they are are notified
// - When peers exist, and a flow controller joins, they become the master, and the other master is demoted

public class ConnectRequestHandler(IConnectionManager connectionManager, SequenceCache sequenceCache, IHubContext hubContext, ILogger logger)
    : IRequestHandler<RequestContextWrapper<ConnectRequest>>
{
    public async Task Handle(
        RequestContextWrapper<ConnectRequest> wrappedRequest,
        CancellationToken cancellationToken)
    {
        var (connectionId, request) = wrappedRequest;

        var peers = connectionManager.GetConnections(request.Partition, request.AirportIdentifier);

        var connection = connectionManager.Add(
            connectionId,
            request.Partition,
            request.AirportIdentifier,
            request.Callsign,
            request.Role);

        logger.LogInformation("{Connection} tracked", connection);

        if (request.Role == Role.Flow)
        {
            // Demote the current master
            var currentMaster = peers.Single(c => c.IsMaster);

            logger.LogInformation("Re-assigning master from {PreviousMaster} to {NewMaster}",
                currentMaster, connection);

            currentMaster.IsMaster = false;
            await hubContext.Clients
                .Client(connectionId)
                .SendAsync(
                    "OwnershipRevoked",
                    new OwnershipRevokedNotification(request.AirportIdentifier),
                    cancellationToken);

            connection.IsMaster = true;
        }
        else if (peers.Length == 0 && connection.Role != Role.Observer)
        {
            // The first connection always becomes the master
            logger.LogInformation("Assigning {Connection} as master", connection);
            connection.IsMaster = true;
        }

        // Broadcast to other clients that this client has connected
        foreach (var peer in peers)
        {
            await hubContext.Clients
                .Client(peer.Id)
                .SendAsync(
                    "PeerConnected",
                    new PeerConnectedNotification(request.AirportIdentifier, request.Callsign, request.Role),
                    cancellationToken);
        }

        var latestSequence = sequenceCache.Get(request.Partition, request.AirportIdentifier);

        await hubContext.Clients
            .Client(connectionId)
            .SendAsync(
                "ConnectionInitialized",
                new ConnectionInitializedNotification(
                    connectionId,
                    request.Partition,
                    request.AirportIdentifier,
                    connection.IsMaster,
                    latestSequence,
                    peers.Select(c => new PeerInfo(c.Callsign, c.Role)).ToArray()),
                cancellationToken);
    }
}
