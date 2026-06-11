using Maestro.Contracts.Connectivity;
using Maestro.Server.Contracts;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public record RequestContextWrapper<T>(string ConnectionId, T Request) : IRequest;

public record RequestContextWrapper<TRequest, TResponse>(string ConnectionId, TRequest Request) : IRequest<TResponse>;

public record NotificationContextWrapper<T>(string ConnectionId, T Notification) : INotification;

public class ConnectRequestHandler(
    IConnectionManager connectionManager,
    SessionCache sessionCache,
    IHubProxy hubProxy,
    ILogger logger)
    : IRequestHandler<RequestContextWrapper<ConnectRequest>>
{
    public async Task Handle(
        RequestContextWrapper<ConnectRequest> wrappedRequest,
        CancellationToken cancellationToken)
    {
        var (connectionId, request) = wrappedRequest;

        var peers = connectionManager.GetConnections(request.Environment, request.AirportIdentifier);

        var connection = connectionManager.Add(
            connectionId,
            request.Version,
            request.Environment,
            request.AirportIdentifier,
            request.Callsign,
            request.Role);

        logger.Information("{Connection} tracked", connection);

        if (connection.Role == Role.Flow)
        {
            var previousMaster = connectionManager.PromoteMaster(connection);
            if (previousMaster is not null)
            {
                logger.Information("Re-assigning master from {PreviousMaster} to {NewMaster}",
                    previousMaster, connection);

                await hubProxy.Send(
                    previousMaster.Id,
                    "OwnershipRevoked",
                    new OwnershipRevokedNotification(request.AirportIdentifier),
                    cancellationToken);
            }
        }
        else if (peers.Length == 0 && connection.Role != Role.Observer)
        {
            // The first connection always becomes the master
            logger.Information("Assigning {Connection} as master", connection);
            connectionManager.PromoteMaster(connection);
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
    }
}
