using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class DestroySessionRequestHandler(ISessionManager sessionManager, IMediator mediator, ILogger logger)
    : IRequestHandler<DestroySessionRequest>
{
    public async Task Handle(DestroySessionRequest request, CancellationToken cancellationToken)
    {
        await sessionManager.DestroySession(request.AirportIdentifier, cancellationToken);

        logger.Information("Session for {AirportIdentifier} destroyed", request.AirportIdentifier);

        await mediator.Publish(
            new SessionDestroyedNotification(request.AirportIdentifier),
            cancellationToken);
    }
}
