using Maestro.Core.Messages;
using Maestro.Plugin.Infrastructure;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class SessionDestroyedNotificationHandler(WindowManager windowManager) : INotificationHandler<SessionDestroyedNotification>
{
    public Task Handle(SessionDestroyedNotification notification, CancellationToken cancellationToken)
    {
        if (!windowManager.TryGetWindow(WindowKeys.Maestro(notification.AirportIdentifier), out var windowHandle))
            return Task.CompletedTask;

        windowHandle.Close();
        return Task.CompletedTask;
    }
}
