using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class SequenceUpdatedNotificationHandler(MaestroViewModel maestroViewModel, DebugViewModel debugViewModel)
    : INotificationHandler<SequenceUpdatedNotification>
{
    public Task Handle(SequenceUpdatedNotification notification, CancellationToken _)
    {
        UpdateSequenceViewModel(notification.Sequence);
        UpdateDebugViewModel(notification.Sequence);

        return Task.CompletedTask;
    }

    void UpdateSequenceViewModel(SequenceMessage sequence)
    {
        var sequenceViewModel = maestroViewModel.Sequences
            .FirstOrDefault(s => s.AirportIdentifier == sequence.AirportIdentifier);

        sequenceViewModel?.UpdateFrom(sequence);
    }

    void UpdateDebugViewModel(SequenceMessage sequence)
    {
        debugViewModel.UpdateFrom(sequence);
    }
}
