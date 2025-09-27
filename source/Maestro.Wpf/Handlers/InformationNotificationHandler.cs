using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Handlers;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class InformationNotificationHandler : INotificationHandler<InformationNotification>
{
    public Task Handle(InformationNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(notification);
        return Task.CompletedTask;
    }
}
