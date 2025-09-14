using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class SessionStartedNotificationHandler(
    ISessionManager sessionManager,
    INotificationStream<PermissionSetChangedNotification> permissionSetChangedNotificationStream)
    : INotificationHandler<SessionStartedNotification>
{
    public async Task Handle(SessionStartedNotification notification, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(notification.AirportIdentifier, cancellationToken);
        var session = lockedSession.Session;

        var permissionSet = new PermissionSet(session.Role, session.Permissions);
        var permissionSetChangedNotification = new PermissionSetChangedNotification(notification.AirportIdentifier, permissionSet);
        await permissionSetChangedNotificationStream.PublishAsync(permissionSetChangedNotification, cancellationToken);
    }
}
