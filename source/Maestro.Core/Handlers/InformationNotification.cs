using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public record InformationNotification(string AirportIdentifier, DateTimeOffset Time, string Message) : INotification;

public class InformationNotificationHandler(ISessionManager sessionManager, INotificationStream<InformationNotification> stream) : INotificationHandler<InformationNotification>
{
    public async Task Handle(InformationNotification notification, CancellationToken cancellationToken)
    {
        if (!sessionManager.HasSessionFor(notification.AirportIdentifier))
            return;

        using var lockedSession = await sessionManager.AcquireSession(notification.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Send(notification, cancellationToken);
        }

        await stream.PublishAsync(notification, cancellationToken);
    }
}
