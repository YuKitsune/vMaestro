using Maestro.Core.Connectivity.Contracts;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public class InitializeConnectionRequestHandler(
    IConnectionManager connectionManager,
    SessionCache sessionCache,
    ILogger logger)
    : IRequestHandler<RequestContextWrapper<InitializeConnectionRequest, InitializeConnectionResponse>, InitializeConnectionResponse>
{
    public Task<InitializeConnectionResponse> Handle(
        RequestContextWrapper<InitializeConnectionRequest, InitializeConnectionResponse> wrappedRequest,
        CancellationToken cancellationToken)
    {
        var connectionId = wrappedRequest.ConnectionId;

        if (!connectionManager.TryGetConnection(connectionId, out var connection))
            throw new InvalidOperationException($"Connection {connectionId} not found");

        var peers = connectionManager.GetConnections(connection.Partition, connection.AirportIdentifier)
            .Where(c => c.Id != connectionId)
            .ToArray();

        var latestSequence = sessionCache.Get(connection.Partition, connection.AirportIdentifier);

        logger.Information("Client {ConnectionId} ({Callsign}) requesting initialization", connectionId, connection.Callsign);

        return Task.FromResult(new InitializeConnectionResponse(
            connectionId,
            connection.Partition,
            connection.AirportIdentifier,
            connection.IsMaster,
            latestSequence,
            peers.Select(c => new PeerInfo(c.Callsign, c.Role)).ToArray()));
    }
}
