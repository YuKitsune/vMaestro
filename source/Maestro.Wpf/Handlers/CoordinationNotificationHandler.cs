using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using CoordinationNotification = Maestro.Core.Messages.CoordinationNotification;

namespace Maestro.Wpf.Handlers;

public class CoordinationNotificationHandler : INotificationHandler<CoordinationNotification>
{
    public Task Handle(CoordinationNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(notification);
        return Task.CompletedTask;
    }
}
