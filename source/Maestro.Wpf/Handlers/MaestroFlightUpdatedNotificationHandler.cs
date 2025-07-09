using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class MaestroFlightUpdatedNotificationHandler(MaestroViewModel maestroViewModel, DebugViewModel debugViewModel)
    : INotificationHandler<MaestroFlightUpdatedNotification>
{
    public Task Handle(MaestroFlightUpdatedNotification notification, CancellationToken _)
    {
        UpdateSequenceViewModel(notification.Flight);
        UpdateDebugViewModel(notification.Flight);

        return Task.CompletedTask;
    }

    void UpdateSequenceViewModel(FlightMessage flight)
    {
        var sequence = maestroViewModel.Sequences
            .FirstOrDefault(s => s.AirportIdentifier == flight.DestinationIdentifier);

        sequence?.UpdateFlight(flight);
    }

    void UpdateDebugViewModel(FlightMessage flight)
    {
        debugViewModel.UpdateFlight(flight);
    }
}
