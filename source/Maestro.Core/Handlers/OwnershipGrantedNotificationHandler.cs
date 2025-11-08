using Maestro.Core.Messages.Connectivity;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class OwnershipGrantedNotificationHandler(ISessionManager sessionManager, ILogger logger)
    : INotificationHandler<OwnershipGrantedNotification>
{
    public async Task Handle(OwnershipGrantedNotification request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        await lockedSession.Session.TakeOwnership(cancellationToken);
        logger.Information("Ownership granted for {AirportIdentifier}", request.AirportIdentifier);
    }
}
