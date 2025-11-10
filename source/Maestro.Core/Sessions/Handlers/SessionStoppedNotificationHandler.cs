using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Core.Handlers;

public class SessionStoppedNotificationHandler(IMaestroConnectionManager connectionManager)
    : INotificationHandler<SessionStoppedNotification>
{
    public async Task Handle(SessionStoppedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection))
            return;

        await connection!.Stop(cancellationToken);
    }
}
