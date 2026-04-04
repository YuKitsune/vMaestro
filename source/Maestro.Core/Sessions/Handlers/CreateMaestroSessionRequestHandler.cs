using Maestro.Core.Sessions.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions.Handlers;

public class CreateMaestroSessionRequestHandler(
    ISessionManager sessionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<CreateMaestroSessionRequest>
{
    public async Task Handle(CreateMaestroSessionRequest request, CancellationToken cancellationToken)
    {
        await sessionManager.CreateSession(request.AirportIdentifier, cancellationToken);

        logger.Information("Session for {AirportIdentifier} created", request.AirportIdentifier);

        await mediator.Publish(new MaestroSessionCreatedNotification(request.AirportIdentifier), cancellationToken);
    }
}
