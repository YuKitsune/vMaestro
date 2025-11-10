using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class StopSessionRequestHandler(ISessionManager sessionManager, IMediator mediator, ILogger logger)
    : IRequestHandler<StopSessionRequest>
{
    public async Task Handle(StopSessionRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        await lockedSession.Session.Stop(cancellationToken);

        logger.Information("Session for {AirportIdentifier} stopped", request.AirportIdentifier);

        await mediator.Publish(
            new SessionStoppedNotification(request.AirportIdentifier),
            cancellationToken);
    }
}
