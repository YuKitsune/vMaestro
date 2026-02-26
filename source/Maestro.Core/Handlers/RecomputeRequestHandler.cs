using Maestro.Core.Configuration;
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
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;
            var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
                .Single(a => a.Identifier == request.AirportIdentifier);

            var flight = sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            // Update the feeder fix estimate in case of a re-route
            var feederFix = flight.Fixes.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
            if (feederFix is not null && feederFix.FixIdentifier == flight.FeederFixIdentifier)
            {
                flight.UpdateFeederFixEstimate(feederFix.Estimate);
                if (feederFix.ActualTimeOver.HasValue && !flight.HasPassedFeederFix)
                {
                    flight.PassedFeederFix(feederFix.ActualTimeOver.Value);
                }
            }

            flight.HighPriority = feederFix is null;
            flight.SetMaximumDelay(null);

            // Reset the runway to the default so it can be calculated in the Scheduling phase
            var runwayMode = sequence.GetRunwayModeAt(flight.LandingEstimate);
            var runwayIdentifier = runwayMode.Default.Identifier;

            // Lookup trajectory for the default runway and current approach type
            var trajectory = trajectoryService.GetTrajectory(flight, runwayIdentifier, flight.ApproachType);

            // Atomic update: runway + trajectory + ETA + STA_FF
            flight.SetRunway(runwayIdentifier, trajectory);
            flight.InvalidateSequenceData();

            // Reset the state
            flight.SetState(State.Unstable, clock);

            sequence.RepositionByEstimate(flight);
            flight.UpdateStateBasedOnTime(clock);

            logger.Information("{Callsign} recomputed", flight.Callsign);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
