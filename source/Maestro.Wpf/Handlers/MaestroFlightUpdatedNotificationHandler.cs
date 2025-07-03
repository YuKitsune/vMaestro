using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class MaestroFlightUpdatedNotificationHandler(ViewModels.MaestroViewModel viewModel)
    : INotificationHandler<MaestroFlightUpdatedNotification>
{
    public Task Handle(MaestroFlightUpdatedNotification notification, CancellationToken _)
    {
        var sequence = viewModel.Sequences
            .FirstOrDefault(s => s.AirportIdentifier == notification.Flight.DestinationIdentifier);

        sequence?.UpdateFlight(notification.Flight);

        return Task.CompletedTask;
    }
}
