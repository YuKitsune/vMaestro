using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using MediatR;

namespace Maestro.Core.Handlers;

public class PeerConnectionHandler(IPeerTracker peerTracker) :
    INotificationHandler<PeerConnectedNotification>,
    INotificationHandler<PeerDisconnectedNotification>
{
    public Task Handle(PeerConnectedNotification notification, CancellationToken cancellationToken)
    {
        peerTracker.AddPeer(notification.AirportIdentifier, new PeerInfo(notification.Callsign, notification.Role));
        return Task.CompletedTask;
    }

    public Task Handle(PeerDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        peerTracker.RemovePeer(notification.AirportIdentifier, notification.Callsign);
        return Task.CompletedTask;
    }
}
