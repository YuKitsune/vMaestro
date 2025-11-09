using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ConnectSessionRequestHandler(ISessionManager sessionManager, IMediator mediator, ILogger logger)
    : IRequestHandler<ConnectSessionRequest>
{
    public async Task Handle(ConnectSessionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);

            // This will take care of connecting (or re-connecting) to the server if the session is active
            await lockedSession.Session.SetConnectionInfo(new ConnectionInfo(request.Partition), cancellationToken);

            if (!lockedSession.Session.IsActive)
                await mediator.Publish(new ConnectionReadyNotification(request.AirportIdentifier), cancellationToken);

            logger.Information("Session for {AirportIdentifier} connected", request.AirportIdentifier);

            await mediator.Publish(
                new SequenceUpdatedNotification(request.AirportIdentifier, lockedSession.Session.Sequence.ToMessage()),
                cancellationToken);
        }
        catch (Exception e)
        {
            await mediator.Publish(new ErrorNotification(e), cancellationToken);
        }
    }
}
