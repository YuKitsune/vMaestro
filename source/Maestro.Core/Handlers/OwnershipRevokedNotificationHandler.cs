using Maestro.Core.Messages.Connectivity;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class OwnershipRevokedNotificationHandler(ISessionManager sessionManager, ILogger logger)
    : INotificationHandler<OwnershipRevokedNotification>
{
    public async Task Handle(OwnershipRevokedNotification request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (!lockedSession.Session.OwnsSequence)
            return;

        await lockedSession.Session.RevokeOwnership(cancellationToken);
        logger.Information("Ownership revoked for {AirportIdentifier}", request.AirportIdentifier);
    }
}
