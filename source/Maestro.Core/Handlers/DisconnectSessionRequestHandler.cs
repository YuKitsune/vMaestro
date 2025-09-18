using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class DisconnectSessionRequestHandler(ISessionManager sessionManager)
    : IRequestHandler<DisconnectSessionRequest>
{
    public async Task Handle(DisconnectSessionRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        await lockedSession.Session.Disconnect(cancellationToken);
    }
}
