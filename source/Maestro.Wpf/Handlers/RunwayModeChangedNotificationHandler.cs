using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class RunwayModeChangedNotificationHandler(MaestroViewModel viewModel)
    : INotificationHandler<RunwayModeChangedNotification>
{
    public Task Handle(RunwayModeChangedNotification notification, CancellationToken cancellationToken)
    {
        var selectedAirport = viewModel.SelectedAirport;
        if (selectedAirport is null)
            return Task.CompletedTask;

        viewModel.CurrentRunwayMode = new RunwayModeViewModel(notification.CurrentRunwayMode);
        viewModel.NextRunwayMode = notification.NextRunwayMode is null
            ? null
            : new RunwayModeViewModel(notification.NextRunwayMode);

        return Task.CompletedTask;
    }
}