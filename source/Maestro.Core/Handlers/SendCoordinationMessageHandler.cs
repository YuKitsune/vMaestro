using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class CoordinationMessageSentNotificationHandler(ISessionManager sessionManager, IClock clock, ILogger logger)
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

            logger.Information("Sending coordination message {Message} to {AirportIdentifier}", notification.Message, notification.AirportIdentifier);
            await lockedSession.Session.Connection.Send(notification, cancellationToken);
        }
    }
}
