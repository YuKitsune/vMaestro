using MediatR;
using TFMS.Core.Dtos.Messages;

namespace TFMS.Wpf.Handlers;

public class SequenceModifiedNotificationHandler(TFMSViewModel viewModel)
    : INotificationHandler<SequenceModifiedNotification>
{
    readonly TFMSViewModel _viewModel = viewModel;

    public Task Handle(SequenceModifiedNotification notification, CancellationToken _)
    {
        var selectedAirport = _viewModel.SelectedAirport;
        if (selectedAirport is null)
            return Task.CompletedTask;

        if (notification.Sequence.AirportIdentifier != selectedAirport.Identifier)
            return Task.CompletedTask;

        _viewModel.Aircraft = notification.Sequence.Arrivals.Select(a =>
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
                MaintainProfileSpeed = true
            })
            .ToList();

        return Task.CompletedTask;
    }
}
