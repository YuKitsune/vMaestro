using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Messages;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record FlightPositionReport(
    string Callsign,
    string Destination,
    FlightPosition Position,
    FixDto[] Estimates)
    :  INotification;

public class FlightPositionReportHandler(ISequenceProvider sequenceProvider, IMediator mediator, IClock clock)
    : INotificationHandler<FlightPositionReport>
{
    public async Task Handle(FlightPositionReport notification, CancellationToken cancellationToken)
    {
        // TODO: Make these configurable
        var vsp = 150;
        var updateRateBeyondRange = TimeSpan.FromMinutes(1);
        var updateRateWithinRange = TimeSpan.FromSeconds(30);
        
        var sequence = sequenceProvider.TryGetSequence(notification.Destination);
        if (sequence is null)
            return;
        
        var flight = await sequence.TryGetFlight(notification.Callsign, cancellationToken);
        if (flight is null)
            return;

        if (!flight.Activated)
            return;
        
        // TODO: What if the aircraft isn't planned via a feeder?
        var feederFix = notification.Estimates.LastOrDefault(x => x.Identifier == flight.FeederFixIdentifier);
        if (feederFix is null)
            return;
        
        // TODO: Calculate distance to feeder (Track miles or direct track?)
        var distanceToFeeder = Calculations.CalculateDistanceNauticalMiles(
            notification.Position.ToCoordinate(),
            feederFix.Position);

        var updateRate = distanceToFeeder > vsp ? updateRateBeyondRange : updateRateWithinRange;
        var shouldUpdate = !flight.PositionUpdated.HasValue ||
                           clock.UtcNow() - flight.PositionUpdated.Value >= updateRate;
        if (!shouldUpdate)
            return;

        flight.UpdatePosition(
            notification.Position,
            notification.Estimates.Select(x => new FixEstimate(x.Identifier, x.Position, x.Estimate)).ToArray(),
            clock);

        var feederFixEstimate = flight.Estimates.SingleOrDefault(x => x.FixIdentifier == flight.FeederFixIdentifier);
        if (feederFixEstimate is not null)
        {
            flight.UpdateFeederFixEstimate(feederFixEstimate.Estimate);
        }
        
        // TODO: Don't calculate ETA here, move it into the sequence.
        //  ETA should be ETA_FF + STAR ETI if a runway and STAR have been provided.
        //  Otherwise use the system provided ETA.
        var landingEstimate = flight.Estimates.Last();
        flight.UpdateLandingEstimate(landingEstimate.Estimate);
        
        await mediator.Publish(new SequenceModifiedNotification(sequence.ToDto()), cancellationToken);
    }
}