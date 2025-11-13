using Maestro.Core.Connectivity;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SessionUpdatedNotificationHandler(
    IMaestroConnectionManager connectionManager,
    IMaestroInstanceManager instanceManager,
    ILogger logger)
    : INotificationHandler<SessionUpdatedNotification>
{
    public async Task Handle(SessionUpdatedNotification notification, CancellationToken cancellationToken)
    {
        // Re-publish sequence updates to the server if we are the master
        if (connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            connection.IsMaster)
        {
            var instance = await instanceManager.GetInstance(notification.AirportIdentifier, cancellationToken);

            var sessionSnapshot = instance.Session.Snapshot();

            logger.Information("Re-publishing session update for {AirportIdentifier}", notification.AirportIdentifier);
            await connection.Send(new SessionUpdatedNotification(sessionSnapshot.AirportIdentifier, sessionSnapshot), cancellationToken);
        }
    }
}
