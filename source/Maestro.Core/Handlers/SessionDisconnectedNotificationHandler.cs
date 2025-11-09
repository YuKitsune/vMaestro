using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SessionDisconnectedNotificationHandler(ISessionManager sessionManager, IMediator mediator, ILogger logger)
    : INotificationHandler<SessionDisconnectedNotification>
{
    public async Task Handle(SessionDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        if (!sessionManager.HasSessionFor(notification.AirportIdentifier))
            return;

        using var lockedSession = await sessionManager.AcquireSession(notification.AirportIdentifier, cancellationToken);
        await lockedSession.Session.TakeOwnership(cancellationToken); // TODO: Connected notification doesn't need to do this since it's handled by it's caller. Need to make this consistent.

        logger.Information("Session for {AirportIdentifier} disconnected, ownership granted", notification.AirportIdentifier);
    }
}
