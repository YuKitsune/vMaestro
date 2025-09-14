using Maestro.Core.Infrastructure;
using Maestro.Core.Messages.Connectivity;
using Maestro.Core.Services;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class PermissionStateNotificationHandler(
    IPermissionService permissionService,
    INotificationStream<PermissionsChangedNotification> notificationStream)
    : INotificationHandler<PermissionsChangedNotification>
{
    public async Task Handle(PermissionsChangedNotification notification, CancellationToken cancellationToken)
    {
        permissionService.UpdatePermissions(notification.Permissions);
        await notificationStream.PublishAsync(notification, cancellationToken);
    }
}