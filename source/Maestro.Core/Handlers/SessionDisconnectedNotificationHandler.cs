using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class SessionDisconnectedNotificationHandler(ISessionManager sessionManager, IMediator mediator)
    : INotificationHandler<SessionDisconnectedNotification>
{
    public async Task Handle(SessionDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        if (!sessionManager.HasSessionFor(notification.AirportIdentifier))
            return;

        using var lockedSession = await sessionManager.AcquireSession(notification.AirportIdentifier, cancellationToken);
    }
}
