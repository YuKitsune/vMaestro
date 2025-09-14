using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class PermissionsChangedNotificationHandler(
    ISessionManager sessionManager,
    INotificationStream<PermissionSetChangedNotification> notificationStream)
    : INotificationHandler<PermissionsChangedNotification>
{
    public async Task Handle(PermissionsChangedNotification notification, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(notification.AirportIdentifier, cancellationToken);
        lockedSession.Session.ChangePermissions(notification.Permissions);

        var permissionSet = new PermissionSet(lockedSession.Session.Role, notification.Permissions);
        await notificationStream.PublishAsync(
            new PermissionSetChangedNotification(notification.AirportIdentifier, permissionSet),
            cancellationToken);
    }
}
