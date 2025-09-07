using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class SequenceUpdatedNotificationHandler(ViewModelManager viewModelManager)
    : INotificationHandler<SequenceUpdatedNotification>
{
    public Task Handle(SequenceUpdatedNotification notification, CancellationToken _)
    {
        UpdateSequenceViewModel(notification.Sequence);
        return Task.CompletedTask;
    }

    void UpdateSequenceViewModel(SequenceMessage sequence)
    {
        var viewModel = viewModelManager.TryGet(sequence.AirportIdentifier);
        viewModel?.UpdateFrom(sequence);
    }
}
