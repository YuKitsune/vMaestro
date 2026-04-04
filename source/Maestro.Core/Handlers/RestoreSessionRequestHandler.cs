using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RestoreSessionRequestHandler(ISessionManager sessionManager, IMediator mediator, ILogger logger)
    : IRequestHandler<RestoreSessionRequest>
{
    public async Task Handle(RestoreSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            session.Restore(request.Session);
            sessionDto = session.Snapshot();
        }

        logger.Debug("Session restored");
        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
