using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class DestroySessionRequestHandler(ISessionManager sessionManager, IMediator mediator)
    : IRequestHandler<DestroySessionRequest>
{
    public async Task Handle(DestroySessionRequest request, CancellationToken cancellationToken)
    {
        await sessionManager.DestroySession(request.AirportIdentifier, cancellationToken);
        await mediator.Publish(
            new SessionDestroyedNotification(request.AirportIdentifier),
            cancellationToken);
    }
}
