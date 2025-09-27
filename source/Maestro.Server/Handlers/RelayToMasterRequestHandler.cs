using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public record RelayToMasterRequest<T>(string MethodName, T Message) : IRequest;

// TODO: Test cases
// - When the connection is not tracked, exception is thrown
// - When the connection is the master, exception is thrown
// - Requests are relayed to the master in an envelope

public class RelayToMasterRequestHandler<T>(IConnectionManager connectionManager, IHubContext<MaestroHub> hubContext, ILogger logger)
    : IRequestHandler<RequestContextWrapper<RelayToMasterRequest<T>, RelayResponse>, RelayResponse>
{
    public async Task<RelayResponse> Handle(RequestContextWrapper<RelayToMasterRequest<T>, RelayResponse> wrappedRequest, CancellationToken cancellationToken)
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

        var envelope = RequestEnvelopeHelper.CreateEnvelope(request.Message, connection.Callsign, connectionId, connection.Role);
        var response = await hubContext.Clients
            .Client(master.Id)
            .InvokeAsync<RelayResponse>(request.MethodName, envelope, cancellationToken);
        if (!response.Success)
        {
            logger.Warning("Received error response from {Connection}: {Error}", master, response.ErrorMessage);
        }

        return response;
    }
}
