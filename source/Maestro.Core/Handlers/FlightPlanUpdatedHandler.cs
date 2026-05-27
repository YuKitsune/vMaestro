using Maestro.Contracts.Flights;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class FlightPlanUpdatedHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IFlightUpdateRateLimiter rateLimiter,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : INotificationHandler<FlightPlanUpdatedNotification>
{
    public async Task Handle(FlightPlanUpdatedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            if (!sessionManager.SessionExists(notification.Destination))
                return;

            logger.Debug("FDR update received for {Callsign}", notification.Callsign);

            var session = await sessionManager.GetSession(notification.Destination, cancellationToken);

            bool isNewFlight;
            using (await session.Semaphore.LockAsync(cancellationToken))
            {
                var isKnownFlight =
                    session.Sequence.FindFlight(notification.Callsign) is not null ||
                    session.PendingFlights.Any(f => f.Callsign == notification.Callsign) ||
                    session.DeSequencedFlights.Any(f => f.Callsign == notification.Callsign);

                if (isKnownFlight &&
                    session.FlightDataRecords.TryGetValue(notification.Callsign, out var existingData))
                {
                    if (!rateLimiter.ShouldUpdate(existingData.LastSeen))
                    {
                        logger.Debug("FDR update for {Callsign} rate-limited", notification.Callsign);
                        return;
                    }
                }

                if (connectionManager.TryGetConnection(notification.Destination, out var connection) &&
                    connection.IsConnected &&
                    !connection.IsMaster)
                {
                    logger.Debug("Relaying FlightPlanUpdatedNotification for {Callsign}", notification.Callsign);
                    await connection.Send(notification, cancellationToken);
                    return;
                }

                session.FlightDataRecords[notification.Callsign] = new FlightDataRecord(
                    notification.Callsign,
                    notification.AircraftType,
                    notification.AircraftCategory,
                    notification.WakeCategory,
                    notification.Origin,
                    notification.Destination,
                    notification.EstimatedDepartureTime,
                    notification.Position,
                    notification.Estimates,
                    clock.UtcNow());

                isNewFlight = !isKnownFlight;
            }

            if (isNewFlight)
            {
                await mediator.Send(
                    new InsertFlightRequest(
                        notification.Destination,
                        notification.Callsign,
                        notification.AircraftType,
                        new FdrInsertionOptions()),
                    cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Error processing FDR update for {Callsign}", notification.Callsign);
        }
    }
}
