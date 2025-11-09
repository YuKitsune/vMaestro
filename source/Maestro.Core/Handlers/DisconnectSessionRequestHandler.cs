using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class DisconnectSessionRequestHandler(ISessionManager sessionManager, IMediator mediator, ILogger logger)
    : IRequestHandler<DisconnectSessionRequest>
{
    public async Task Handle(DisconnectSessionRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        await lockedSession.Session.Disconnect(cancellationToken);

        logger.Information("Disconnected session for {AirportIdentifier}", request.AirportIdentifier);

        if (!lockedSession.Session.IsActive)
            await mediator.Publish(new ConnectionUnreadyNotification(request.AirportIdentifier), cancellationToken);
        else
            await mediator.Publish(new SessionDisconnectedNotification(request.AirportIdentifier, IsReady: false), cancellationToken);
    }
}
