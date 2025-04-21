using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public class HandleSequenceModified(DebugViewModel debugViewModel) : INotificationHandler<SequenceModifiedNotification>
{
    public Task Handle(SequenceModifiedNotification notification, CancellationToken cancellationToken)
    {
        debugViewModel.Flights = notification.Sequence.Flights
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
                    a.TotalDelay,
                    a.RemainingDelay))
            .ToList();
        
        return Task.CompletedTask;
    }
}

public partial class DebugViewModel : ObservableObject
{
    [ObservableProperty]
    List<FlightViewModel> _flights = [];
}