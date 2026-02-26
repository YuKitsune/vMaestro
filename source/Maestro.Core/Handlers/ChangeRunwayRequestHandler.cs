using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeRunwayRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IArrivalLookup arrivalLookup,
    ITrajectoryService trajectoryService,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeRunwayRequest>
{
    public async Task Handle(ChangeRunwayRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ChangeRunwayRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var flight = sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            // TODO: Track who initiated the change
            logger.Information("Changing runway for {Callsign} to {NewRunway}.", request.Callsign, request.RunwayIdentifier);

            // Determine the approach type for the new runway
            var approachTypes = arrivalLookup.GetApproachTypes(
                flight.DestinationIdentifier,
                flight.FeederFixIdentifier,
                flight.Fixes.Select(x => x.FixIdentifier).ToArray(),
                request.RunwayIdentifier,
                flight.AircraftType,
                flight.AircraftCategory);

            var newApproachType = approachTypes.Contains(flight.ApproachType)
                ? flight.ApproachType
                : approachTypes.FirstOrDefault() ?? string.Empty;

            // Lookup trajectory for the new runway and approach before updating flight
            var trajectory = trajectoryService.GetTrajectory(flight, request.RunwayIdentifier, newApproachType);

            flight.SetRunway(request.RunwayIdentifier, trajectory);

            // Update approach type if it changed
            if (flight.ApproachType != newApproachType)
                flight.SetApproachType(newApproachType, trajectory);

            // Unstable flights become Stable when changing runway
            if (flight.State is State.Unstable)
                flight.SetState(State.Stable, clock);

            sequence.RepositionByEstimate(flight);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
