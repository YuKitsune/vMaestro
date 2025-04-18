using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public record FlightPositionReport(
    string Callsign,
    string Destination,
    FlightPosition Position,
    FixEstimate[] Estimates)
    :  INotification;

public class FlightPositionReportHandler(
    ISequenceProvider sequenceProvider,
    IFixLookup fixLookup,
    IMediator mediator,
    IClock clock,
    ILogger<FlightPositionReportHandler> logger)
    : INotificationHandler<FlightPositionReport>
{
    public async Task Handle(FlightPositionReport notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Received position report for {Callsign}", notification.Callsign);
        
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
        var feederFix = fixLookup.FindFix(flight.FeederFixIdentifier);
        if (feederFix is null)
            return;
        
        // TODO: Calculate distance to feeder (Track miles or direct track?)
        var distanceToFeeder = Calculations.CalculateDistanceNauticalMiles(
            notification.Position.Coordinate,
            feederFix.Coordinate);

        var updateRate = distanceToFeeder > vsp ? updateRateBeyondRange : updateRateWithinRange;
        var shouldUpdate = !flight.PositionUpdated.HasValue ||
                           clock.UtcNow() - flight.PositionUpdated.Value >= updateRate;
        if (!shouldUpdate)
            return;
        
        logger.LogDebug("Updating position for {Callsign}", notification.Callsign);

        flight.UpdatePosition(
            notification.Position,
            notification.Estimates.ToArray(),
            clock);
    }
}