using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class FlightLandedNotificationHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : INotificationHandler<FlightLandedNotification>
{
    public async Task Handle(FlightLandedNotification notification, CancellationToken cancellationToken)
    {
        var session = await sessionManager.GetSession(notification.AirportIdentifier, cancellationToken);
        var flight = session.Sequence.FindFlight(notification.Callsign);
        if (flight is null)
        {
            logger.Debug("FlightLandedNotification received for a {Callsign} who is not in the {AirportIdentifier} sequence", notification.Callsign,  notification.AirportIdentifier);
            return;
        }

        if (connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying FlightLandedNotification for {AirportIdentifier}", notification.AirportIdentifier);
            await connection.Send(notification, cancellationToken);
            return;
        }

        var runway = session.Sequence.CurrentRunwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier);
        if (runway is null)
        {
            logger.Information("{Callsign} landed on an off-mode runway, cannot update achieved rates", notification.Callsign);
            return;
        }

        SessionDto sessionDto;
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            session.LandingStatistics.RecordLandingTime(
                runway,
                notification.ActualLandingTime,
                clock);

            sessionDto = session.Snapshot();

            var achievedRate = session.LandingStatistics.AchievedLandingRates[runway.Identifier];
            if (achievedRate is AchievedRate rate)
                logger.Information(
                    "{Callsign} landed on RWY {Runway} — avg {Average:F0}s, deviation {Deviation:+F0;-F0;0}s from {Target:F0}s target",
                    notification.Callsign, runway.Identifier,
                    rate.AverageLandingInterval.TotalSeconds,
                    rate.LandingIntervalDeviation.TotalSeconds,
                    runway.AcceptanceRate.TotalSeconds);
            else
                logger.Information(
                    "{Callsign} landed on RWY {Runway} — insufficient data for rate calculation",
                    notification.Callsign, runway.Identifier);
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
