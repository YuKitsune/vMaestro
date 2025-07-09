using Maestro.Core.Messages;
using Maestro.Wpf.ViewModels;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class FlightRemovedNotificationHandler(MaestroViewModel maestroViewModel, DebugViewModel debugViewModel) : INotificationHandler<FlightRemovedNotification>
{
    public Task Handle(FlightRemovedNotification notification, CancellationToken cancellationToken)
    {
        UpdateSequenceViewModel(notification.AirportIdentifier, notification.Callsign);
        UpdateDebugViewModel(notification.Callsign);

        return Task.CompletedTask;
    }

    void UpdateSequenceViewModel(string airportIdentifier, string callsign)
    {
        var sequence = maestroViewModel.Sequences
            .FirstOrDefault(s => s.AirportIdentifier == airportIdentifier);

        sequence?.RemoveFlight(callsign);
    }

    void UpdateDebugViewModel(string callsign)
    {
        debugViewModel.RemoveFlight(callsign);
    }
}
