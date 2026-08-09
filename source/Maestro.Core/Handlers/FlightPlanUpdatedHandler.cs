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

            if (connectionManager.TryGetConnection(notification.Destination, out var connection) &&
                connection!.IsConnected &&
                !connection.IsMaster)
            {
                return;
            }

            var session = await sessionManager.GetSession(notification.Destination, cancellationToken);

            // Creation: store the latest flight plan data
            FlightDataRecord newRecord;
            bool alreadyActivated;
            using (await session.Semaphore.LockAsync(cancellationToken))
            {
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

            if (alreadyActivated)
                return;

            // Activation check: determine whether the flight should be automatically activated
            var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(notification.Destination);
            var shouldActivate = ShouldAutoActivate(newRecord, airportConfiguration, clock.UtcNow());

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

    static bool ShouldAutoActivate(FlightDataRecord record, AirportConfiguration config, DateTimeOffset now)
    {
        // AutoActivateDepartures is a hard opt-out for departure-airport flights. When disabled, a
        // flight originating from a configured departure airport is never auto-activated and must
        // be inserted manually from the Pending List.
        if (config.DepartureAirports.Any(d => d.Identifier == record.Origin) &&
            !config.AutoActivateDepartures)
        {
            return false;
        }

        // Require a live position before auto-activating a flight
        if (record.Position is null)
            return false;

        // Don't activate a flight that is still on the ground. Departure-airport flights stay in the
        // Pending List until they are airborne, and arrival flights that have already touched down
        // must not be re-inserted.
        if (record.Position.IsOnGround)
            return false;

        var landingEstimate = record.Estimates.LastOrDefault()?.Estimate;
        if (landingEstimate is not null)
        {
            var timeToLanding = landingEstimate.Value - now;
            if (timeToLanding > TimeSpan.FromMinutes(config.MaximumAutoActivationLeadTimeMinutes))
                return false;
        }

        if (record.EstimatedFlightTime < TimeSpan.FromMinutes(config.MinimumAutoActivationFlightTimeMinutes))
            return false;

        if (record.State is not FlightPlanState.Active)
            return false;

        // Only auto-activate flights that still have a feeder fix ahead of them. A flight already
        // inside the TMA (past the feeder fix) or filed without a feeder fix must be inserted
        // manually, so that a flight the controller has removed cannot resurrect itself while
        // absorbing delay on final.
        return record.Estimates.Any(e =>
            config.FeederFixes.Contains(e.FixIdentifier) && e.Estimate > now);
    }
}
