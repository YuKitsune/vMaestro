using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class SequenceUpdatedNotificationHandler(MaestroViewModel maestroViewModel)
    : INotificationHandler<SequenceUpdatedNotification>
{
    public Task Handle(SequenceUpdatedNotification notification, CancellationToken _)
    {
        UpdateSequenceViewModel(notification.Sequence);

        return Task.CompletedTask;
    }

    void UpdateSequenceViewModel(SequenceMessage sequence)
    {
        var sequenceViewModel = maestroViewModel.Sequences
            .FirstOrDefault(s => s.AirportIdentifier == sequence.AirportIdentifier);

        sequenceViewModel?.UpdateFrom(sequence);
    }
}
