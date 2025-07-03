using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class RunwayModeChangedNotificationHandler(MaestroViewModel viewModel)
    : INotificationHandler<RunwayModeChangedNotification>
{
    public Task Handle(RunwayModeChangedNotification notification, CancellationToken cancellationToken)
    {
        var sequence = viewModel.Sequences
            .FirstOrDefault(s => s.AirportIdentifier == notification.AirportIdentifier);
        
        if (sequence is null)
            return Task.CompletedTask;

        sequence.CurrentRunwayMode = new RunwayModeViewModel(notification.CurrentRunwayMode);
        sequence.NextRunwayMode = notification.NextRunwayMode is null
            ? null
            : new RunwayModeViewModel(notification.NextRunwayMode);

        return Task.CompletedTask;
    }
}