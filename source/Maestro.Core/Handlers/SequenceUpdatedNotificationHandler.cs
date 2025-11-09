using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SequenceUpdatedNotificationHandler(ISessionManager sessionManager, ILogger logger)
    : INotificationHandler<SequenceUpdatedNotification>
{
    public async Task Handle(SequenceUpdatedNotification notification, CancellationToken cancellationToken)
    {
        // Re-publish sequence updates to the server
        using var lockedSession = await sessionManager.AcquireSession(notification.Sequence.AirportIdentifier, cancellationToken);
        if (lockedSession.Session.OwnsSequence)
        {
            logger.Information("Re-publishing sequence update for {AirportIdentifier}", notification.Sequence.AirportIdentifier);
            lockedSession.Session.Connection?.Send(notification, cancellationToken);
        }
        else
        {
            logger.Information("Restoring sequence for {AirportIdentifier} from server", notification.Sequence.AirportIdentifier);
            lockedSession.Session.Sequence.Restore(notification.Sequence);
        }
    }
}
