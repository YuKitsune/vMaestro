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
        if (config.DepartureAirports.Any(d => d.Identifier == record.Origin))
            return config.AutoActivateDepartures;

        // Don't reactivate a flight that has already touched down. After landing the FDR
        // continues to report Active during taxi until it transitions to STATE_FINISHED,
        // which would otherwise re-insert a flight that a controller has just removed.
        if (record.Position?.IsOnGround == true)
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

        // Require at least one future-dated route estimate. Once the route is fully overflown
        // the FDR can still report Active briefly, and activating without a future estimate
        // produces a flight whose entire trajectory is in the past.
        return record.Estimates.Any(e => e.Estimate > now);
    }
}
