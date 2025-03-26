using MediatR;
using Microsoft.Extensions.Logging;
using TFMS.Core.DTOs;
using TFMS.Core.Model;

namespace TFMS.Core.Handlers;

public class FDRUpdatedNotificationHandler(IMediator mediator, SequenceProvider sequenceProvider)
    : INotificationHandler<FDRUpdatedNotification>
{
    readonly IMediator mediator = mediator;
    readonly SequenceProvider _sequenceProvider = sequenceProvider;
    // readonly ILogger<FDRUpdatedNotificationHandler> _logger = logger;

    public Task Handle(FDRUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var sequence = _sequenceProvider.Sequences.FirstOrDefault(s => s.AirportIdentifier == notification.FlightDataRecord.DestinationIcaoCode);
        if (sequence == null)
        {
            sequence = new Sequence(mediator, notification.FlightDataRecord.DestinationIcaoCode);
            _sequenceProvider.Sequences.Add(sequence);
        }

        // TODO: Source feeders from sequence
        var feederFix = notification.FlightDataRecord.Estimates.FirstOrDefault(f => Configuration.Demo.Airports.Single().FeederFixes.Contains(f.Identifier));
        if (feederFix is null)
        {
            // _logger.LogWarning($"Could not find feeder fix for {notification.FlightDataRecord.Callsign} arriving at {notification.FlightDataRecord.DestinationIcaoCode}");
            return Task.CompletedTask;
        }

        // TODO: Source landing time from somewhere else
        var arrivalEta = notification.FlightDataRecord.Estimates.Last().Estimate;

        var arrival = sequence.Arrivals.FirstOrDefault(a => a.Callsign == notification.FlightDataRecord.Callsign);
        if (arrival is not null)
        {
            // TODO: Recompute if the runway has changed
            if (notification.FlightDataRecord.AssignedRunway is not null)
                arrival.AssignRunway(notification.FlightDataRecord.AssignedRunway);

            arrival.UpdateFeederFixEstimate(feederFix.Estimate);
            arrival.UpdateEstimatedLandingTime(arrivalEta);
        }
        else
        {
            sequence.Add(
                notification.FlightDataRecord.Callsign,
                notification.FlightDataRecord.OriginIcaoCode,
                notification.FlightDataRecord.DestinationIcaoCode,
                notification.FlightDataRecord.AssignedRunway,
                null, //TODO,
                feederFix.Estimate,
                arrivalEta);
        }

        return Task.CompletedTask;
    }
}
