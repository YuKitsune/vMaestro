using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public record RelayToMasterRequest(string MethodName, IRequest Message) : IRequest;

public class RelayToMasterRequestHandler(IConnectionManager connectionManager, IHubProxy hubProxy, ILogger logger)
    : IRequestHandler<RequestContextWrapper<RelayToMasterRequest, RelayResponse>, RelayResponse>
{
    public async Task<RelayResponse> Handle(RequestContextWrapper<RelayToMasterRequest, RelayResponse> wrappedRequest, CancellationToken cancellationToken)
    {
        var (connectionId, request) = wrappedRequest;
        if (!connectionManager.TryGetConnection(connectionId, out var connection))
        {
            throw new InvalidOperationException($"Connection {connectionId} is not tracked");
        }

        if (connection.IsMaster)
        {
            logger.Warning("{Connection} attempted to relay to itself", connection);
            return RelayResponse.CreateFailure("Cannot relay to self");
        }

        var peers = connectionManager.GetPeers(connection);
        var master = peers.SingleOrDefault(c => c.IsMaster);
        if (master is null)
        {
            logger.Error("No master found");
            return RelayResponse.CreateFailure("No master found");
        }

        var envelope = new RequestEnvelope
        {
            OriginatingCallsign = connection.Callsign,
            OriginatingConnectionId = connectionId,
            OriginatingRole = connection.Role,
            Request = request.Message
        };

        var response = await hubProxy.Invoke<RequestEnvelope, RelayResponse>(master.Id, request.MethodName, envelope, cancellationToken);
        if (!response.Success)
        {
            logger.Warning("Received error response from {Connection}: {Error}", master, response.ErrorMessage);
        }

        return response;
    }
}
