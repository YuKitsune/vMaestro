using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SessionUpdatedNotificationHandler(
    IMaestroConnectionManager connectionManager,
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
            logger.Information("Re-publishing session update for {AirportIdentifier}", notification.AirportIdentifier);
            await connection.Send(notification, cancellationToken);
        }
    }
}
