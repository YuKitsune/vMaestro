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

        if (connection.Role != Role.Observer)
        {
            var currentMaster = peers.SingleOrDefault(c => c.IsMaster);
            if (currentMaster is null)
            {
                logger.Information("Assigning {Connection} as master", connection);
                connectionManager.PromoteMaster(connection);
            }
            else if (GetMasterPriority(request.Role) > GetMasterPriority(currentMaster.Role))
            {
                logger.Information("Re-assigning master from {CurrentMaster} to {NewMaster}",
                    currentMaster, connection);

                connectionManager.PromoteMaster(connection);

                await hubProxy.Send(
                    currentMaster.Id,
                    "OwnershipRevoked",
                    new OwnershipRevokedNotification(request.AirportIdentifier),
                    cancellationToken);
            }
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

    static int GetMasterPriority(Role role) => role switch
    {
        Role.Flow => 3,
        Role.Enroute => 2,
        Role.Approach => 1,
        _ => 0
    };
}
