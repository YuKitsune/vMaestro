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

            if (connectionManager.TryGetConnection(notification.Destination, out var connection) &&
                connection!.IsConnected &&
                !connection.IsMaster)
            {
                if (session.FlightDataRecords.TryGetValue(notification.Callsign, out var existingData) &&
                    !rateLimiter.ShouldUpdateFlight(existingData.LastSeen))
                {
                    logger.Debug("FDR update for {Callsign} rate-limited", notification.Callsign);
                    return;
                }

                logger.Debug("Relaying FlightPlanUpdatedNotification for {Callsign}", notification.Callsign);
                await connection.Send(notification, cancellationToken);
                return;
            }

            bool isNewFlight;
            using (await session.Semaphore.LockAsync(cancellationToken))
            {
                if (session.FlightDataRecords.TryGetValue(notification.Callsign, out var existingData) &&
                    !rateLimiter.ShouldUpdateFlight(existingData.LastSeen))
                {
                    logger.Debug("FDR update for {Callsign} rate-limited", notification.Callsign);
                    return;
                }

                isNewFlight = !session.FlightDataRecords.ContainsKey(notification.Callsign);
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
            }

            if (!isNewFlight)
                return;

            // Do this outside the lock to avoid a deadlock
            await mediator.Send(
                new InsertFlightRequest(
                    notification.Destination,
                    notification.Callsign,
                    notification.AircraftType,
                    new FdrInsertionOptions()),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Error processing FDR update for {Callsign}", notification.Callsign);
        }
    }
}
