using CommunityToolkit.Mvvm.Messaging;
using Maestro.Contracts.Sessions;
using MediatR;

namespace Maestro.Avalonia.Handlers;

public class SessionUpdatedNotificationHandler : INotificationHandler<SessionUpdatedNotification>
{
    public Task Handle(SessionUpdatedNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(notification);
        return Task.CompletedTask;
    }
}
