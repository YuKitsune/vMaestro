using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class CoordinationMessageSentNotificationHandler(
    IMaestroConnectionManager connectionManager,
    IClock clock,
    ILogger logger)
    : INotificationHandler<CoordinationMessageSentNotification>
{
    public async Task Handle(CoordinationMessageSentNotification request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected)
        {
            var notification = new CoordinationNotification(
                request.AirportIdentifier,
                clock.UtcNow(),
                request.Message,
                request.Destination);

            logger.Information("Sending coordination message {Message} to {AirportIdentifier}", notification.Message, notification.AirportIdentifier);
            await connection.Send(notification, cancellationToken);
        }
    }
}
