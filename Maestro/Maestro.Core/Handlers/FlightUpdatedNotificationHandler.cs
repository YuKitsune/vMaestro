using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class FlightUpdatedNotificationHandler(
    IMediator mediator,
    SequenceProvider sequenceProvider,
    IAirportConfigurationProvider airportConfigurationProvider)
    : INotificationHandler<FlightUpdatedNotification>
{
    readonly IMediator mediator = mediator;
    readonly SequenceProvider _sequenceProvider = sequenceProvider;
    readonly IAirportConfigurationProvider _airportConfigurationProvider = airportConfigurationProvider;
    // readonly ILogger<FDRUpdatedNotificationHandler> _logger = logger;

    public async Task Handle(FlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var sequence = _sequenceProvider.Sequences.FirstOrDefault(s => s.AirportIdentifier == notification.DestinationIcao);
        if (sequence == null)
        {
            var airportConfigs = _airportConfigurationProvider.GetAirportConfigurations();

            var airportConfig = airportConfigs.FirstOrDefault(x => x.Identifier == notification.DestinationIcao);
            if (airportConfig is null)
            {
                // _logger.LogDebug($"Ignoring {notification.FlightDataRecord.Callsign}. No airport config exists for destination.");
                return;
            }

            sequence = new Sequence(mediator, notification.DestinationIcao, airportConfig.FeederFixes);
            _sequenceProvider.Sequences.Add(sequence);
        }

        var feederFix = notification.Estimates.FirstOrDefault(f => sequence.FeederFixes.Contains(f.Identifier));
        if (feederFix is null)
        {
            // _logger.LogWarning($"Could not find feeder fix for {notification.FlightDataRecord.Callsign} arriving at {notification.FlightDataRecord.DestinationIcaoCode}");
            return;
        }

        // TODO: Source landing time from somewhere else
        var arrivalEta = notification.Estimates.Last().Estimate;

        var arrival = sequence.Arrivals.FirstOrDefault(a => a.Callsign == notification.Callsign);
        if (arrival is not null)
        {
            // TODO: Recompute if the runway has changed
            if (notification.AssignedRunway is not null)
                arrival.AssignRunway(notification.AssignedRunway);

            arrival.UpdateFeederFixEstimate(feederFix.Estimate);
            arrival.UpdateLanidngEstimate(arrivalEta);
        }
        else
        {
            var flight = new Flight(
                notification.Callsign,
                notification.AircraftType,
                notification.OriginIcao,
                notification.DestinationIcao,
                feederFix.Identifier,
                notification.AssignedRunway,
                notification.AssignedStar,
                feederFix.Estimate,
                arrivalEta);

            sequence.Add(flight);
        }

        await mediator.Publish(new SequenceModifiedNotification(sequence.ToDTO()));
    }
}
