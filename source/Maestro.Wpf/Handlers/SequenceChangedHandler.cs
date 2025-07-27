using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class SequenceChangedHandler(MaestroViewModel maestroViewModel)
    : INotificationHandler<SequenceChangedNotification>
{
    public Task Handle(SequenceChangedNotification notification, CancellationToken cancellationToken)
    {
        var sequence = maestroViewModel.Sequences
            .SingleOrDefault(s => s.AirportIdentifier == notification.Sequence.AirportIdentifier);

        sequence?.UpdateSequence(notification.Sequence);
        return Task.CompletedTask;
    }
}
