using Maestro.Contracts.Flights;
using Maestro.Core.Configuration;
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
    IAirportConfigurationProvider airportConfigurationProvider,
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

            // TODO test case: When slave connection, notification is relayed to master
            if (connectionManager.TryGetConnection(notification.Destination, out var connection) &&
                connection!.IsConnected &&
                !connection.IsMaster)
            {
                if (session.FlightDataRecords.TryGetValue(notification.Callsign, out var existingData) &&
                    !rateLimiter.ShouldUpdate(existingData.LastSeen))
                {
                    logger.Debug("FDR update for {Callsign} rate-limited", notification.Callsign);
                    return;
                }

                logger.Debug("Relaying FlightPlanUpdatedNotification for {Callsign}", notification.Callsign);
                await connection.Send(notification, cancellationToken);
                return;
            }

            // Creation: store the latest flight plan data
            FlightDataRecord newRecord;
            bool alreadyActivated;
            using (await session.Semaphore.LockAsync(cancellationToken))
            {
                // TODO test case: When subsequent updates are too close, rate limit applies
                if (session.FlightDataRecords.TryGetValue(notification.Callsign, out var existingData) &&
                    !rateLimiter.ShouldUpdate(existingData.LastSeen))
                {
                    logger.Debug("FDR update for {Callsign} rate-limited", notification.Callsign);
                    return;
                }

                // TODO test case: When FDR is not known, it is created
                // TODO test case: When FDR is known, it is updated
                newRecord = new FlightDataRecord(
                    notification.Callsign,
                    notification.AircraftType,
                    notification.AircraftCategory,
                    notification.WakeCategory,
                    notification.Origin,
                    notification.Destination,
                    notification.EstimatedDepartureTime,
                    notification.EstimatedFlightTime,
                    notification.State,
                    notification.Position,
                    notification.Estimates,
                    clock.UtcNow());
                session.FlightDataRecords[notification.Callsign] = newRecord;

                alreadyActivated = session.Sequence.FindFlight(notification.Callsign) is not null
                    || session.DeSequencedFlights.Any(f => f.Callsign == notification.Callsign);
            }

            // TODO test case: When flight is already active, another one is not activated.
            if (alreadyActivated)
                return;

            // Activation check: determine whether the flight should be automatically activated
            var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(notification.Destination);
            bool shouldActivate;
            try
            {
                shouldActivate = ShouldAutoActivate(newRecord, airportConfiguration);
            }
            catch (NotImplementedException)
            {
                logger.Warning("Auto-activation criteria not yet implemented, skipping activation for {Callsign}", notification.Callsign);
                return;
            }

            // TODO test case: When flight is not active, and the FDR becomes active, flight is activated
            // TODO test case: When flight is not active, and the FDR is not active, flight is not activated
            // TODO test case: When flight is not active, and it is beyond the required EET threshold, flight is activated
            // TODO test case: When flight is not active, and it is within the required EET threshold, flight is not activated
            // TODO test case: When flight is not active, and it departs from a departure airport, flight is activated
            if (shouldActivate)
            {
                // Do this outside the lock to avoid a deadlock
                await mediator.Send(
                    new ActivateFlightRequest(notification.Destination, notification.Callsign),
                    cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Error processing FDR update for {Callsign}", notification.Callsign);
        }
    }

    static bool ShouldAutoActivate(FlightDataRecord record, AirportConfiguration config)
    {
        if (record.EstimatedFlightTime < TimeSpan.FromMinutes(config.MinimumAutoActivationFlightTimeMinutes))
            return false;

        if (record.State is FlightPlanState.Active)
            return true;

        return false;
    }
}
