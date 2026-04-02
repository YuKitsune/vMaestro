using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class FlightLandedNotificationHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : INotificationHandler<FlightLandedNotification>
{
    public async Task Handle(FlightLandedNotification notification, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying FlightLandedNotification for {AirportIdentifier}", notification.AirportIdentifier);
            await connection.Send(notification, cancellationToken);
            return;
        }

        // TODO: Maybe move this up to avoid telling the master about irrelevant flights?
        var instance = await instanceManager.GetInstance(notification.AirportIdentifier, cancellationToken);
        var flight = instance.Session.Sequence.FindFlight(notification.Callsign);
        if (flight is null)
        {
            logger.Debug("FlightLandedNotification received for a {Callsign} who is not in the {AirportIdentifier} sequence", notification.Callsign,  notification.AirportIdentifier);
            return;
        }

        var runway = instance.Session.Sequence.CurrentRunwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier);
        if (runway is null)
        {
            logger.Information("{Callsign} landed on an off-mode runway, cannot update achieved rates", notification.Callsign);
            return;
        }

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            instance.Session.LandingStatistics.RecordLandingTime(
                runway,
                notification.ActualLandingTime,
                clock);
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                instance.Session.Snapshot()),
            cancellationToken);
    }
}
