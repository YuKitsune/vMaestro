using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class ConnectionInitializedNotificationHandler(ISessionManager sessionManager, IPeerTracker peerTracker, IMediator mediator)
    : INotificationHandler<ConnectionInitializedNotification>
{
    public async Task Handle(ConnectionInitializedNotification notification, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(notification.AirportIdentifier, cancellationToken);
        if (notification.IsMaster)
        {
            await lockedSession.Session.TakeOwnership(cancellationToken);
        }
        else
        {
            await lockedSession.Session.RevokeOwnership(cancellationToken);
        }

        foreach (var peers in notification.ConnectedPeers)
        {
            peerTracker.AddPeer(notification.AirportIdentifier, peers);
        }

        // TODO: Clear the sequence if it's null
        if (notification.Sequence is not null)
        {
            lockedSession.Session.Sequence.Restore(notification.Sequence);
            await mediator.Publish(
                new SequenceUpdatedNotification(notification.AirportIdentifier, lockedSession.Session.Sequence.ToMessage()),
                cancellationToken);
        }

        await mediator.Publish(
            new SessionConnectedNotification(
                notification.AirportIdentifier,
                lockedSession.Session.Connection.Role,
                notification.ConnectedPeers),
            cancellationToken);
    }
}
