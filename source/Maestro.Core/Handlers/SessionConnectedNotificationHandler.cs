using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class SessionConnectedNotificationHandler(ISessionManager sessionManager, IPeerTracker peerTracker, IMediator mediator)
    : INotificationHandler<SessionConnectedNotification>, INotificationHandler<SessionStartedNotification>
{
    public async Task Handle(SessionConnectedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var connectedPeer in notification.Peers)
        {
            peerTracker.AddPeer(notification.AirportIdentifier, connectedPeer);
        }

        await TryPublishInformationNotification(notification.AirportIdentifier, cancellationToken);
    }

    public async Task Handle(SessionStartedNotification notification, CancellationToken cancellationToken)
    {
        await TryPublishInformationNotification(notification.AirportIdentifier, cancellationToken);
    }

    async Task TryPublishInformationNotification(string airportIdentifier, CancellationToken cancellationToken)
    {
        if (!sessionManager.HasSessionFor(airportIdentifier))
            return;

        using var lockedSession = await sessionManager.AcquireSession(airportIdentifier, cancellationToken);

        if (lockedSession.Session is { IsActive: true, IsConnected: true })
        {
            await mediator.Publish(new InformationNotification(airportIdentifier, DateTimeOffset.UtcNow, "Connected to Maestro server."), cancellationToken);
        }
    }
}
