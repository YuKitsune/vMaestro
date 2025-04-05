using Maestro.Core.Dtos.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class SequenceModifiedNotificationHandler(ViewModels.MaestroViewModel viewModel)
    : INotificationHandler<SequenceModifiedNotification>
{
    readonly ViewModels.MaestroViewModel _viewModel = viewModel;

    public Task Handle(SequenceModifiedNotification notification, CancellationToken _)
    {
        var selectedAirport = _viewModel.SelectedAirport;
        if (selectedAirport is null)
            return Task.CompletedTask;

        if (notification.Sequence.AirportIdentifier != selectedAirport.Identifier)
            return Task.CompletedTask;

        _viewModel.RefreshSequence(notification.Sequence);

        return Task.CompletedTask;
    }
}
