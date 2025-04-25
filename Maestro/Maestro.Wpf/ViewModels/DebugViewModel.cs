using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public class HandleSequenceModified(DebugViewModel debugViewModel) : INotificationHandler<MaestroFlightUpdatedNotification>
{
    public Task Handle(MaestroFlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var flights = debugViewModel.Flights.ToList();
        var index = flights.FindIndex(f => f.Callsign == notification.Flight.Callsign);
        var viewModel = new FlightViewModel(
            notification.Flight.Callsign,
            notification.Flight.AircraftType,
            notification.Flight.WakeCategory,
            notification.Flight.OriginIdentifier,
            notification.Flight.DestinationIdentifier,
            notification.Flight.State,
            -1, // TODO:
            notification.Flight.FeederFixIdentifier,
            notification.Flight.InitialFeederFixTime,
            notification.Flight.EstimatedFeederFixTime,
            notification.Flight.ScheduledFeederFixTime,
            notification.Flight.AssignedRunwayIdentifier,
            -1, // TODO:
            notification.Flight.InitialLandingTime,
            notification.Flight.EstimatedLandingTime,
            notification.Flight.ScheduledLandingTime,
            notification.Flight.TotalDelay,
            notification.Flight.RemainingDelay,
            notification.Flight.FlowControls);
        
        if (index != -1)
        {
            flights[index] = viewModel;
        }
        else
        {
            flights.Add(viewModel);
        }
        
        debugViewModel.Flights = flights;
        
        return Task.CompletedTask;
    }
}

public partial class DebugViewModel : ObservableObject
{
    [ObservableProperty]
    List<FlightViewModel> _flights = [];
}