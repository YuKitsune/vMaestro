using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class CreateSessionRequestHandler(
    ISessionManager sessionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<CreateSessionRequest>
{
    public async Task Handle(CreateSessionRequest request, CancellationToken cancellationToken)
    {
        await sessionManager.CreateSession(request.AirportIdentifier, cancellationToken);

        logger.Information("Session for {AirportIdentifier} created", request.AirportIdentifier);

        await mediator.Publish(new SessionCreatedNotification(request.AirportIdentifier), cancellationToken);
    }
}
