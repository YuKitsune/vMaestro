using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class MoveFlightRequestHandler(
    ISessionManager sessionManager,
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

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;
            var flight = sequence.FindFlight(request.Callsign);
            if (flight is null)
                throw new MaestroException($"{request.Callsign} not found");

            var newIndex = sequence.IndexOf(request.NewLandingTime);

            flight.SetTargetLandingTime(request.NewLandingTime);

            sequence.ThrowIsTimeIsUnavailable(request.Callsign, request.NewLandingTime, request.RunwayIdentifier);

            // TODO: Manually set the runway for now, but we need to revisit this later
            // Re: delaying into a new runway mode

            var fixNames = session.FlightDataRecords.TryGetValue(flight.Callsign, out var flightDataRecord)
                ? flightDataRecord.Estimates.Select(x => x.FixIdentifier).ToArray()
                : [];

            // Lookup trajectory for the new runway and approach before updating flight
            var trajectory = trajectoryService.GetTrajectory(
                flight,
                request.RunwayIdentifier,
                flight.ApproachType,
                fixNames,
                session.Sequence.UpperWind);

            flight.SetRunway(request.RunwayIdentifier, trajectory);

            logger.Verbose(
                "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
                flight.Callsign,
                runway.Identifier,
                runway.ApproachType,
                trajectory.NormalTimeToGo,
                trajectory.PressureTimeToGo,
                trajectory.MaxPressureTimeToGo);

            flight.InvalidateSequenceData();

            // Unstable flights become stable when moved
            if (flight.State == State.Unstable)
                flight.SetState(airportConfiguration.ManualInteractionState, clock);

            sequence.Move(flight, newIndex);

            logger.Information("Flight {Callsign} moved to {NewLandingTime}", flight.Callsign, flight.LandingTime);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
