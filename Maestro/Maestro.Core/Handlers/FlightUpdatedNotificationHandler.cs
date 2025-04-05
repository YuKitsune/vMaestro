using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class FlightUpdatedNotificationHandler(
    IMediator mediator,
    SequenceProvider sequenceProvider)
    : INotificationHandler<FlightUpdatedNotification>
{
    readonly IMediator _mediator = mediator;
    readonly SequenceProvider _sequenceProvider = sequenceProvider;
    // readonly ILogger<FlightUpdatedNotificationHandler> _logger = logger;

    public async Task Handle(FlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        if (!_sequenceProvider.TryGetSequence(notification.Destination, out var sequence))
            return;
        
        var feederFix = notification.Estimates.LastOrDefault(f => sequence.FeederFixes.Contains(f.Identifier));
        var arrivalEta = notification.Estimates.Last().Estimate;

        var existingFlight = await sequence.TryGetFlight(notification.Callsign, cancellationToken);
        if (existingFlight is not null)
        {
            if (feederFix is not null)
                existingFlight.UpdateFeederFixEstimate(feederFix.Estimate);
            
            // TODO: Calculate landing estimate based on ETA_FF + STAR ETI
            // TODO: Calculate landing estimate based on ETA_FF + GS / STAR Distance
            existingFlight.UpdateLandingEstimate(arrivalEta);
            
            // TODO: What else needs to be updated
            
            // TODO: Do we need to raise a sequence update here?
        }

        var newFlight = new Flight
        {
            Callsign = notification.Callsign,
            AircraftType = notification.AircraftType,
            OriginIdentifier = notification.Origin,
            DestinationIdentifier = notification.Destination,
            FeederFixIdentifier = feederFix?.Identifier,
            AssignedRunwayIdentifier = notification.AssignedRunway,
            AssignedStarIdentifier = notification.AssignedStar,
            HighPriority = feederFix is null, // A flight will be made HighPriority if it does not plan via any FF
        };
        
        newFlight.UpdateLandingEstimate(arrivalEta);

        await sequence.Add(newFlight, cancellationToken);
    }
}
