using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

// BUG: This doesn't really do anything unless the session is already started.
// This means uses need to start the session (connect to VATSIM) before calling this.
// Need to think about how this should work.

public class ConnectSessionRequestHandler(ISessionManager sessionManager, IMaestroConnectionFactory maestroConnectionFactory, IMediator mediator)
    : IRequestHandler<ConnectSessionRequest>
{
    public async Task Handle(ConnectSessionRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);

        var connection = maestroConnectionFactory.Create(request.AirportIdentifier, request.Partition);
        await lockedSession.Session.Connect(connection, cancellationToken);

        await mediator.Publish(
            new SequenceUpdatedNotification(request.AirportIdentifier, lockedSession.Session.Sequence.ToMessage()),
            cancellationToken);
    }
}
