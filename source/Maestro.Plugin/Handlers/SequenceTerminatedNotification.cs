using Maestro.Core.Messages;
using Maestro.Plugin.Infrastructure;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class SequenceTerminatedNotificationHandler(WindowManager windowManager) : INotificationHandler<SequenceTerminatedNotification>
{
    public Task Handle(SequenceTerminatedNotification notification, CancellationToken cancellationToken)
    {
        if (!windowManager.TryGetWindow(WindowKeys.Maestro(notification.AirportIdentifier), out var windowHandle))
            return Task.CompletedTask;

        windowHandle.Close();

        return Task.CompletedTask;
    }
}
