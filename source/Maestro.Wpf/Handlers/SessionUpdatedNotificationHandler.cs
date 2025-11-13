using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class SessionUpdatedNotificationHandler : INotificationHandler<SessionUpdatedNotification>
{
    public Task Handle(SessionUpdatedNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(notification);
        return Task.CompletedTask;
    }
}
