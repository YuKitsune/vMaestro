using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class MaestroFlightUpdatedNotificationHandler(ViewModels.MaestroViewModel viewModel)
    : INotificationHandler<MaestroFlightUpdatedNotification>
{
    public Task Handle(MaestroFlightUpdatedNotification notification, CancellationToken _)
    {
        var selectedAirport = viewModel.SelectedAirport;
        if (selectedAirport is null)
            return Task.CompletedTask;

        if (notification.Flight.DestinationIdentifier != selectedAirport.Identifier)
            return Task.CompletedTask;

        viewModel.UpdateFlight(notification.Flight);

        return Task.CompletedTask;
    }
}
