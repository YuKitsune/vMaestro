using MediatR;
using Microsoft.AspNetCore.SignalR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

// TODO: Test cases
// - When the connection is not tracked, exception is thrown
// - When the connection is the mater, exception is thrown
// - Flight updates are relayed to the master

public class FlightUpdatedNotificationHandler(IConnectionManager connectionManager, IHubContext<MaestroHub> hubContext, ILogger logger)
    : INotificationHandler<NotificationContextWrapper<FlightUpdatedNotificationHandler>>
{
    public async Task Handle(NotificationContextWrapper<FlightUpdatedNotificationHandler> wrappedNotification, CancellationToken cancellationToken)
    {
        var (connectionId, notification) = wrappedNotification;

        if (!connectionManager.TryGetConnection(connectionId, out var connection))
        {
            throw new InvalidOperationException($"Connection {connectionId} is not tracked");
        }

        if (connection.IsMaster)
        {
            throw new InvalidOperationException("Cannot relay to master");
        }

        var peers = connectionManager.GetPeers(connection);
        var master = peers.SingleOrDefault(c => c.IsMaster);
        if (master is null)
        {
            throw new InvalidOperationException("No master found");
        }

        logger.Information("{Connection} relaying flight update to {Master}", connection, master);
        await hubContext.Clients
            .Client(master.Id)
            .SendAsync("FlightUpdated", notification, cancellationToken);
    }
}
