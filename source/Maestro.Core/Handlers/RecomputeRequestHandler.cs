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

public class RecomputeRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    ITrajectoryService trajectoryService,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<RecomputeRequest>
{
    public async Task Handle(RecomputeRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying RecomputeRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;
            var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

            var flight = sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            // Recalculate the feeder fix in case of a re-route
            instance.Session.FlightDataRecords.TryGetValue(flight.Callsign, out var flightDataRecord);
            var feederFix = flightDataRecord?.Estimates.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
            var landingEstimate = flightDataRecord?.Estimates.LastOrDefault()?.Estimate ?? flight.LandingEstimate;


            flight.HighPriority = feederFix is null;
            flight.SetMaximumDelay(null);

            // Reset the runway to the default so it can be calculated in the Scheduling phase
            var runwayMode = sequence.GetRunwayModeAt(landingEstimate);
            var runway = runwayMode.Default;

            var fixNames = flightDataRecord?.Estimates.Select(e => e.FixIdentifier).ToArray() ?? [];

            // Lookup trajectory for the (possibly new) feeder fix + default runway + default approach type
            var trajectory = trajectoryService.GetTrajectory(
                flight.GetPerformanceData(),
                flight.DestinationIdentifier,
                feederFix?.FixIdentifier,
                runway.Identifier,
                runway.ApproachType,
                fixNames,
                instance.Session.Sequence.UpperWind);

            // Update feeder fix (may have changed due to re-routing)
            flight.SetFeederFix(
                feederFix?.FixIdentifier,
                trajectory,
                feederFix?.Estimate,
                landingEstimate);

            flight.SetRunway(runway.Identifier, trajectory);
            flight.SetApproachType(runway.ApproachType, trajectory);

            flight.InvalidateSequenceData();

            // Reset the state
            flight.SetState(State.Unstable, clock);

            sequence.RepositionByEstimate(flight);
            flight.UpdateStateBasedOnTime(clock, airportConfiguration);

            logger.Information("{Callsign} recomputed", flight.Callsign);

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
