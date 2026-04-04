using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class MoveFlightRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    ITrajectoryService trajectoryService,
    IPerformanceLookup performanceLookup,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : IRequestHandler<MoveFlightRequest>
{
    public async Task Handle(MoveFlightRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying MoveFlightRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Moving {Callsign} to {NewLandingTime:HHmm} at {AirportIdentifier}", request.Callsign, request.NewLandingTime, request.AirportIdentifier);

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);
        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;
            var flight = sequence.FindFlight(request.Callsign);
            if (flight is null)
                throw new MaestroException($"{request.Callsign} not found");

            var newIndex = sequence.IndexOf(request.NewLandingTime);

            flight.SetTargetLandingTime(request.NewLandingTime);

            sequence.ThrowIsTimeIsUnavailable(request.Callsign, request.NewLandingTime, request.RunwayIdentifier);

            // TODO: Manually set the runway for now, but we need to revisit this later
            // Re: delaying into a new runway mode

            var fixNames = instance.Session.FlightDataRecords.TryGetValue(flight.Callsign, out var flightDataRecord)
                ? flightDataRecord.Estimates.Select(x => x.FixIdentifier).ToArray()
                : [];

            // Lookup trajectory for the new runway and approach before updating flight
            var trajectory = trajectoryService.GetTrajectory(
                flight,
                request.RunwayIdentifier,
                flight.ApproachType,
                fixNames,
                instance.Session.Sequence.UpperWind);

            flight.SetRunway(request.RunwayIdentifier, trajectory);

            logger.Verbose(
                "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
                flight.Callsign,
                flight.AssignedRunwayIdentifier,
                flight.ApproachType,
                trajectory.TimeToGo,
                trajectory.Pressure,
                trajectory.MaxPressure);

            flight.InvalidateSequenceData();

            // Unstable flights become stable when moved
            if (flight.State == State.Unstable)
                flight.SetState(airportConfiguration.ManualInteractionState, clock);

            sequence.Move(flight, newIndex);

            logger.Information("Flight {Callsign} moved to {NewLandingTime}", flight.Callsign, flight.LandingTime);

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
