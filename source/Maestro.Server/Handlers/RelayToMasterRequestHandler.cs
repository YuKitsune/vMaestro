using Maestro.Core.Connectivity.Contracts;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public record RelayToMasterRequest(string MethodName, IRelayableRequest Message) : IRequest;

public class RelayToMasterRequestHandler(IConnectionManager connectionManager, IHubProxy hubProxy, ILogger logger)
    : IRequestHandler<RequestContextWrapper<RelayToMasterRequest, ServerResponse>, ServerResponse>
{
    public async Task<ServerResponse> Handle(RequestContextWrapper<RelayToMasterRequest, ServerResponse> wrappedRequest, CancellationToken cancellationToken)
    {
        var (connectionId, request) = wrappedRequest;
        if (!connectionManager.TryGetConnection(connectionId, out var connection))
        {
            throw new InvalidOperationException($"Connection {connectionId} is not tracked");
        }

        if (connection.IsMaster)
        {
            logger.Warning("{Connection} attempted to relay to itself", connection);
            return ServerResponse.CreateFailure("Cannot relay to self");
        }

        var peers = connectionManager.GetPeers(connection);
        var master = peers.SingleOrDefault(c => c.IsMaster);
        if (master is null)
        {
            logger.Error("No master found");
            return ServerResponse.CreateFailure("No master found");
        }

        var envelope = new RequestEnvelope
        {
            OriginatingCallsign = connection.Callsign,
            OriginatingConnectionId = connectionId,
            OriginatingRole = connection.Role,
            Request = request.Message
        };

        var response = await hubProxy.Invoke<RequestEnvelope, ServerResponse>(master.Id, request.MethodName, envelope, cancellationToken);
        if (!response.Success)
        {
            logger.Warning("Received error response from {Connection}: {Error}", master, response.ErrorMessage);
        }

        return response;
    }
}
