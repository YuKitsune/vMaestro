using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class StopSessionRequestHandler(ISessionManager sessionManager, IMediator mediator)
    : IRequestHandler<StopSessionRequest>
{
    public async Task Handle(StopSessionRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        await lockedSession.Session.Stop(cancellationToken);
        await mediator.Publish(
            new SessionStoppedNotification(request.AirportIdentifier),
            cancellationToken);
    }
}
