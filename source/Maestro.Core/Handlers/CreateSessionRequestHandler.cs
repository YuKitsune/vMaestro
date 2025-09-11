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
        if (!string.IsNullOrEmpty(request.Partition))
        {
            await sessionManager.CreateRemoteSession(request.AirportIdentifier, request.Partition, cancellationToken);
        }
        else
        {
            await sessionManager.CreateLocalSession(request.AirportIdentifier, cancellationToken);
        }

        await mediator.Publish(new SessionCreatedNotification(request.AirportIdentifier), cancellationToken);
    }
}
