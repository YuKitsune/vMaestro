using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class DisconnectSessionRequestHandler(ISessionManager sessionManager, IMediator mediator)
    : IRequestHandler<DisconnectSessionRequest>
{
    public async Task Handle(DisconnectSessionRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        await lockedSession.Session.Disconnect(cancellationToken);

        if (!lockedSession.Session.IsActive)
            await mediator.Publish(new ConnectionUnreadyNotification(request.AirportIdentifier), cancellationToken);
        else
            await mediator.Publish(new SessionDisconnectedNotification(request.AirportIdentifier, IsReady: false), cancellationToken);
    }
}
