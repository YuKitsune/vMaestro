using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ConnectionInitializedNotificationHandler(
    ISessionManager sessionManager,
    IPeerTracker peerTracker,
    IMediator mediator,
    ILogger logger)
    : INotificationHandler<ConnectionInitializedNotification>
{
    public async Task Handle(ConnectionInitializedNotification notification, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(notification.AirportIdentifier, cancellationToken);
        if (notification.IsMaster)
        {
            logger.Information("Connection initialized for {AirportIdentifier} as master", notification.AirportIdentifier);
            await lockedSession.Session.TakeOwnership(cancellationToken);
        }
        else
        {
            logger.Information("Connection initialized for {AirportIdentifier} as slave", notification.AirportIdentifier);
            await lockedSession.Session.RevokeOwnership(cancellationToken);
        }

        foreach (var peers in notification.ConnectedPeers)
        {
            peerTracker.AddPeer(notification.AirportIdentifier, peers);
        }

        // TODO: Clear the sequence if it's null
        if (notification.Sequence is not null)
        {
            logger.Information("Restoring sequence for {AirportIdentifier} from server", notification.AirportIdentifier);
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
