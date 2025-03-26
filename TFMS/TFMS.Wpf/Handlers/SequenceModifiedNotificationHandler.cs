using MediatR;
using TFMS.Core.DTOs;

namespace TFMS.Wpf.Handlers;

public class SequenceModifiedNotificationHandler(TFMSViewModel viewModel) : INotificationHandler<SequenceModifiedNotification>
{
    readonly TFMSViewModel _viewModel = viewModel;

    public Task Handle(SequenceModifiedNotification notification, CancellationToken _)
    {
        if (notification.Sequence.AirportIdentifier != "YSSY")
            return Task.CompletedTask;

        _viewModel.Aircraft = notification.Sequence.Arrivals.Select(a =>
            new AircraftViewModel
            {
                Callsign = a.Callsign,

                // TODO: Figure out which times to use here.
                LandingTime = a.EstimatedLandingTime,
                FeederFixTime = a.EstimatedFeederFixTime,

                Runway = a.AssignedRunway,
                TotalDelay = a.TotalDelay ?? TimeSpan.Zero,
                RemainingDelay = a.RemainingDelay ?? TimeSpan.Zero,
                MaintainProfileSpeed = true
            })
            .ToList();

        return Task.CompletedTask;
    }
}
