using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Coordination;
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
