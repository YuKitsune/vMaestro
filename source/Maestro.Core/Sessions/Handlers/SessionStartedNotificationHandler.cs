using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Core.Handlers;

public class SessionStartedNotificationHandler(IMaestroConnectionManager connectionManager)
    : INotificationHandler<SessionStartedNotification>
{
    public async Task Handle(SessionStartedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection))
            return;

        await connection!.Start(notification.Position, cancellationToken);
    }
}
