using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class DebugViewModel : ObservableObject, INotificationHandler<SequenceModifiedNotification>
{
    [ObservableProperty]
    List<FlightViewModel> _flights = [];
    
    public Task Handle(SequenceModifiedNotification notification, CancellationToken cancellationToken)
    {
        Flights = notification.Sequence.Flights
            .Select(a =>
                new FlightViewModel(
                    a.Callsign,
                    a.AircraftType,
                    a.WakeCategory,
                    a.OriginIdentifier,
                    a.DestinationIdentifier,
                    a.State,
                    -1, // TODO:
                    a.FeederFixIdentifier,
                    a.InitialFeederFixTime,
                    a.EstimatedFeederFixTime,
                    a.ScheduledFeederFixTime,
                    a.AssignedRunwayIdentifier,
                    -1, // TODO:
                    a.InitialLandingTime,
                    a.EstimatedLandingTime,
                    a.ScheduledLandingTime,
                    a.TotalDelayToRunway,
                    a.RemainingDelayToRunway))
            .ToList();
        
        return Task.CompletedTask;
    }
}