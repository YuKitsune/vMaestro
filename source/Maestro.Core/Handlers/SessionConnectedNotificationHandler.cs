using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SessionConnectedNotificationHandler(ISessionManager sessionManager, IPeerTracker peerTracker, IMediator mediator, ILogger logger)
    : INotificationHandler<SessionConnectedNotification>
{
    public async Task Handle(SessionConnectedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var connectedPeer in notification.Peers)
        {
            peerTracker.AddPeer(notification.AirportIdentifier, connectedPeer);
        }

        await TryPublishInformationNotification(notification.AirportIdentifier, cancellationToken);
        logger.Information("Session for {AirportIdentifier} connected", notification.AirportIdentifier);
    }

    async Task TryPublishInformationNotification(string airportIdentifier, CancellationToken cancellationToken)
    {
        if (!sessionManager.HasSessionFor(airportIdentifier))
            return;

        using var lockedSession = await sessionManager.AcquireSession(airportIdentifier, cancellationToken);

        if (lockedSession.Session is { IsActive: true, IsConnected: true })
        {
            await mediator.Publish(
                new CoordinationNotification(
                    airportIdentifier,
                    DateTimeOffset.UtcNow,
                    "Connected to Maestro server.",
                    new CoordinationDestination.Controller(lockedSession.Session.Position)),
                cancellationToken);
        }
    }
}
