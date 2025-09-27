using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class CreateSessionRequestHandler(
    ISessionManager sessionManager,
    IMediator mediator)
    : IRequestHandler<CreateSessionRequest>
{
    public async Task Handle(CreateSessionRequest request, CancellationToken cancellationToken)
    {
        await sessionManager.CreateSession(request.AirportIdentifier, cancellationToken);
        await mediator.Publish(new SessionCreatedNotification(request.AirportIdentifier), cancellationToken);
    }
}
