using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class SequenceUpdatedNotificationHandler : INotificationHandler<SequenceUpdatedNotification>
{
    public Task Handle(SequenceUpdatedNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(notification);
        return Task.CompletedTask;
    }
}
