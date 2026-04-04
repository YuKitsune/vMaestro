using Maestro.Core.Sessions.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions.Handlers;

public class DestroyMaestroSessionRequestHandler(
    ISessionManager sessionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<DestroyMaestroSessionRequest>
{
    public async Task Handle(DestroyMaestroSessionRequest request, CancellationToken cancellationToken)
    {
        await sessionManager.DestroySession(request.AirportIdentifier, cancellationToken);

        logger.Information("Session for {AirportIdentifier} destroyed", request.AirportIdentifier);

        await mediator.Publish(
            new MaestroSessionDestroyedNotification(request.AirportIdentifier),
            cancellationToken);
    }
}
