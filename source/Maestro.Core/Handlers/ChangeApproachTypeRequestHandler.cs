using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeApproachTypeRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    ITrajectoryService trajectoryService,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeApproachTypeRequest>
{
    public async Task Handle(ChangeApproachTypeRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ChangeApproachTypeRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Changing approach type for {Callsign} to {NewApproachType} at {AirportIdentifier}", request.Callsign, request.ApproachType, request.AirportIdentifier);

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var flight = sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            var fixNames = instance.Session.FlightDataRecords.TryGetValue(flight.Callsign, out var flightDataRecord)
                ? flightDataRecord.Estimates.Select(x => x.FixIdentifier).ToArray()
                : [];

            // Lookup trajectory for the new approach type
            var trajectory = trajectoryService.GetTrajectory(
                flight,
                flight.AssignedRunwayIdentifier,
                request.ApproachType,
                fixNames,
                instance.Session.Sequence.UpperWind);

            flight.SetApproachType(request.ApproachType, trajectory);

            logger.Verbose(
                "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
                flight.Callsign,
                flight.AssignedRunwayIdentifier,
                request.ApproachType,
                trajectory.TimeToGo,
                trajectory.Pressure,
                trajectory.MaxPressure);

            logger.Information("{Callsign} approach type changed to {ApproachType}", flight.Callsign, request.ApproachType);

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
