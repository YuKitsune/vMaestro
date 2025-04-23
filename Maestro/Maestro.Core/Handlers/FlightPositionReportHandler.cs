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
    ILogger<FlightPositionReportHandler> logger)
    : INotificationHandler<FlightPositionReport>
{
    public async Task Handle(FlightPositionReport notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Received position report for {Callsign}", notification.Callsign);
        
        var sequence = sequenceProvider.TryGetSequence(notification.Destination);
        if (sequence is null)
            return;
        
        var flight = await sequence.TryGetFlight(notification.Callsign, cancellationToken);
        if (flight is null)
            return;

        if (!flight.Activated)
            return;
        
        logger.LogDebug("Updating position for {Callsign}", notification.Callsign);

        flight.UpdatePosition(
            notification.Position,
            notification.Estimates.ToArray());
    }
}