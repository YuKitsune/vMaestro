using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;
using CoordinationNotification = Maestro.Core.Messages.CoordinationNotification;

namespace Maestro.Wpf.Handlers;

public class CoordinationNotificationHandler(ISessionManager sessionManager, ILogger logger) : INotificationHandler<CoordinationNotification>
{
    public Task Handle(CoordinationNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(notification);
        return Task.CompletedTask;
    }
}
