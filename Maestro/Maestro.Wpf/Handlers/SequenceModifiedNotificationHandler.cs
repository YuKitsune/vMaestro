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

        _viewModel.Aircraft = notification.Sequence.Flights.Select(a =>
            new AircraftViewModel
            {
                Callsign = a.Callsign,
                FeederFix = a.FeederFix,
                Runway = a.AssignedRunway,

                // TODO: Figure out which times to use here.
                LandingTime = a.EstimatedLandingTime,
                FeederFixTime = a.EstimatedFeederFixTime,
                
                TotalDelay = a.InitialLandingTime - a.ScheduledLandingTime, // TODO
                RemainingDelay = a.EstimatedLandingTime - a.ScheduledLandingTime, // TODO
            })
            .ToList();

        return Task.CompletedTask;
    }
}
