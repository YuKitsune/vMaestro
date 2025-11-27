using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class CoordinationNotificationHandler : INotificationHandler<CoordinationMessageReceivedNotification>
{
    public Task Handle(CoordinationMessageReceivedNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(notification);
        return Task.CompletedTask;
    }
}
