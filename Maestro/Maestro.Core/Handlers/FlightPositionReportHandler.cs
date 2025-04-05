using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Messages;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record FlightPositionReport(
    string Callsign,
    string Destination,
    Position Position,
    FixDto[] Estimates)
    :  INotification;

public class FlightPositionReportHandler(SequenceProvider sequenceProvider, IMediator mediator, IClock clock)
    : INotificationHandler<FlightPositionReport>
{
    public async Task Handle(FlightPositionReport notification, CancellationToken cancellationToken)
    {
        // TODO: Make configurable
        var vsp = 150;
        var updateRateBeyondRange = TimeSpan.FromMinutes(1); // TODO: What should this be?
        var updateRateWithinRange = TimeSpan.FromSeconds(30);
        
        var sequence = sequenceProvider.TryGetSequence(notification.Destination);
        if (sequence is null)
            return;
        
        var flight = await sequence.TryGetFlight(notification.Callsign, cancellationToken);
        if (flight is null)
            return;

        // TODO: Calculate distance to feeder (Track miles or direct track?)
        // TODO: What if the aircraft isn't planned via a feeder?
        var distanceToFeeder = 50;
        var updateRate = distanceToFeeder > vsp ? updateRateBeyondRange : updateRateWithinRange;
        var shouldUpdate = !flight.PositionUpdated.HasValue ||
                           clock.UtcNow() - flight.PositionUpdated.Value >= updateRate;
        if (!shouldUpdate)
            return;

        flight.UpdatePosition(
            notification.Position,
            notification.Estimates.Select(x =>
                    new FixEstimate
                    {
                        FixIdentifier = x.Identifier,
                        Estimate = x.Estimate,
                    })
                .ToArray(),
            clock);

        // TODO: Is this the right place to provide estimates?
        var feederFixEstimate = flight.Estimates.SingleOrDefault(x => x.FixIdentifier == flight.FeederFixIdentifier);
        if (feederFixEstimate is not null)
        {
            flight.UpdateFeederFixEstimate(feederFixEstimate.Estimate);
        }
        
        var landingEstimate = flight.Estimates.Last();
        flight.UpdateLandingEstimate(landingEstimate.Estimate);
        
        await mediator.Publish(new SequenceModifiedNotification(sequence.ToDto()), cancellationToken);
    }
}