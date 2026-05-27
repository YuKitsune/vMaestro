using Maestro.Contracts.Flights;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Handlers;

public class FlightPlanUpdatedNotificationHandler(IConnectionManager connectionManager, IHubProxy hubProxy, ILogger logger)
    : INotificationHandler<NotificationContextWrapper<FlightPlanUpdatedNotification>>
{
    public async Task Handle(NotificationContextWrapper<FlightPlanUpdatedNotification> wrappedNotification, CancellationToken cancellationToken)
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

        logger.Debug("{Connection} relaying flight plan update to {Master}", connection, master);
        await hubProxy.Send(master.Id, "FlightPlanUpdated", notification, cancellationToken);
    }
}
