using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class SequenceUpdatedNotificationHandler(ISessionManager sessionManager)
    : INotificationHandler<SequenceUpdatedNotification>
{
    public async Task Handle(SequenceUpdatedNotification notification, CancellationToken cancellationToken)
    {
        // Re-publish sequence updates to the server
        using var lockedSession = await sessionManager.AcquireSession(notification.Sequence.AirportIdentifier, cancellationToken);
        if (lockedSession.Session.OwnsSequence)
            lockedSession.Session.Connection?.Send(notification, cancellationToken);
    }
}
