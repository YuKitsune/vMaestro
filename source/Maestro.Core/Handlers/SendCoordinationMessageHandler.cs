using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class CoordinationMessageSentNotificationHandler(ISessionManager sessionManager, IClock clock)
    : INotificationHandler<CoordinationMessageSentNotification>
{
    public async Task Handle(CoordinationMessageSentNotification request, CancellationToken cancellationToken)
    {
        if (!sessionManager.HasSessionFor(request.AirportIdentifier))
            return;

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { Connection: not null })
        {
            var notification = new CoordinationNotification(
                request.AirportIdentifier,
                clock.UtcNow(),
                request.Message,
                request.Destination);

            await lockedSession.Session.Connection.Send(notification, cancellationToken);
        }
    }
}
