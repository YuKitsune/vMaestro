using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class StartSessionRequestHandler(ISessionManager sessionManager, IMediator mediator)
    : IRequestHandler<StartSessionRequest>
{
    public async Task Handle(StartSessionRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        await lockedSession.Session.Start(request.Position, cancellationToken);

        // TODO: Get results from initialization
        await mediator.Publish(
            new SessionStartedNotification(
                request.AirportIdentifier,
                request.Position),
            cancellationToken);
    }
}
