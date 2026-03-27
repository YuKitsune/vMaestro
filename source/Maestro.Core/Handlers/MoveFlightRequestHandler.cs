using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class MoveFlightRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    ITrajectoryService trajectoryService,
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
            logger.Information("Relaying MoveFlightRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

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

            var runwayMode = sequence.GetRunwayModeAt(request.NewLandingTime);
            var runway = runwayMode.Runways.FirstOrDefault(r => request.RunwayIdentifiers.Contains(r.Identifier))
                         ?? runwayMode.Default;

            sequence.ThrowIsTimeIsUnavailable(request.Callsign, request.NewLandingTime, runway.Identifier);

            // TODO: Manually set the runway for now, but we need to revisit this later
            // Re: delaying into a new runway mode

            var fixNames = instance.Session.FlightDataRecords.TryGetValue(flight.Callsign, out var flightDataRecord)
                ? flightDataRecord.Estimates.Select(x => x.FixIdentifier).ToArray()
                : [];

            // Lookup trajectory for the new runway and approach before updating flight
            var trajectory = trajectoryService.GetTrajectory(
                flight,
                runway.Identifier,
                runway.ApproachType,
                fixNames,
                instance.Session.Sequence.UpperWind);

            // Atomic update: runway + trajectory + ETA + STA_FF
            flight.SetRunway(runway.Identifier, trajectory);

            // Update approach type if it changed
            if (flight.ApproachType != runway.ApproachType)
                flight.SetApproachType(runway.ApproachType, trajectory);

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
