using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public record RequestContextWrapper<T>(string ConnectionId, T Request) : IRequest;

public record RequestContextWrapper<TRequest, TResponse>(string ConnectionId, TRequest Request) : IRequest<TResponse>;

public record NotificationContextWrapper<T>(string ConnectionId, T Notification) : INotification;

public class ConnectRequestHandler(
    IConnectionManager connectionManager,
    SequenceCache sequenceCache,
    IHubProxy hubProxy,
    ILogger logger)
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

        logger.Information("{Connection} tracked", connection);

        if (request.Role == Role.Flow)
        {
            // Demote the current master
            var currentMaster = peers.SingleOrDefault(c => c.IsMaster);
            if (currentMaster is not null)
            {
                logger.Information("Re-assigning master from {PreviousMaster} to {NewMaster}",
                    currentMaster, connection);
                currentMaster.IsMaster = false;

                await hubProxy.Send(
                    currentMaster.Id,
                    "OwnershipRevoked",
                    new OwnershipRevokedNotification(request.AirportIdentifier),
                    cancellationToken);
            }

            connection.IsMaster = true;
        }
        else if (peers.Length == 0 && connection.Role != Role.Observer)
        {
            // The first connection always becomes the master
            logger.Information("Assigning {Connection} as master", connection);
            connection.IsMaster = true;
        }

        // Broadcast to other clients that this client has connected
        foreach (var peer in peers)
        {
            await hubProxy.Send(
                peer.Id,
                "PeerConnected",
                new PeerConnectedNotification(request.AirportIdentifier, request.Callsign, request.Role),
                cancellationToken);
        }

        var latestSequence = sequenceCache.Get(request.Partition, request.AirportIdentifier);

        await hubProxy.Send(
            connectionId,
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
