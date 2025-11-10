using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SequenceUpdatedNotificationHandler(
    IMaestroConnectionManager connectionManager,
    ISessionManager sessionManager,
    ILogger logger)
    : INotificationHandler<SequenceUpdatedNotification>
{
    public async Task Handle(SequenceUpdatedNotification notification, CancellationToken cancellationToken)
    {
        // Re-publish sequence updates to the server if we are the master
        if (connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            connection.IsMaster)
        {
            // Offline mode, nothing to do
            logger.Information("Re-publishing sequence update for {AirportIdentifier}", notification.Sequence.AirportIdentifier);
            await connection.Send(notification, cancellationToken);
        }
    }
}
