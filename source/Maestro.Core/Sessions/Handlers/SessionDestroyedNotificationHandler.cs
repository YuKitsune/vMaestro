using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Core.Handlers;

public class SessionDestroyedNotificationHandler(IMaestroConnectionManager connectionManager)
    : INotificationHandler<SessionDestroyedNotification>
{
    public async Task Handle(SessionDestroyedNotification notification, CancellationToken cancellationToken)
    {
        await connectionManager.RemoveConnection(notification.AirportIdentifier, cancellationToken);
    }
}
