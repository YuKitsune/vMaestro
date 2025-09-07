using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class SequenceUpdatedNotificationHandler(INotificationStream<SequenceUpdatedNotification> notificationStream)
    : INotificationHandler<SequenceUpdatedNotification>
{
    public async Task Handle(SequenceUpdatedNotification notification, CancellationToken cancellationToken)
    {
        await notificationStream.PublishAsync(notification, cancellationToken);
    }
}
