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
    IEstimateProvider estimateProvider,
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

            // Reset the runway so it can be calculated in the Scheduling phase
            flight.SetRunway(string.Empty, manual: false);

            // Reset the feeder fix in case of a re-route
            var feederFix = flight.Fixes.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
            if (feederFix is not null)
                flight.SetFeederFix(feederFix.FixIdentifier, feederFix.Estimate, feederFix.ActualTimeOver);

            flight.HighPriority = feederFix is null;
            flight.SetMaximumDelay(null);

            CalculateEstimates(airportConfiguration, flight);
            flight.InvalidateSequenceData();

            sequence.RepositionByEstimate(flight);

            // Reset the state
            flight.SetState(State.Unstable, clock);
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

    // TODO: This is copied from FlightUpdatedHandler, consider refactoring
    void CalculateEstimates(AirportConfiguration airportConfiguration, Flight flight)
    {
        var feederFixSystemEstimate = flight.Fixes.LastOrDefault(e => e.FixIdentifier == flight.FeederFixIdentifier);
        if (!flight.HasPassedFeederFix && feederFixSystemEstimate?.ActualTimeOver is not null)
        {
            flight.PassedFeederFix(feederFixSystemEstimate.ActualTimeOver.Value);
            logger.Information(
                "{Callsign} passed {FeederFix} at {ActualTimeOver}",
                flight.Callsign,
                flight.FeederFixIdentifier,
                feederFixSystemEstimate.ActualTimeOver);
        }

        // Don't update ETA_FF once passed FF
        if (feederFixSystemEstimate is not null && !flight.HasPassedFeederFix)
        {
            var calculatedFeederFixEstimate = estimateProvider.GetFeederFixEstimate(
                airportConfiguration,
                flight.FeederFixIdentifier!,
                feederFixSystemEstimate!.Estimate,
                flight.Position);
            if (calculatedFeederFixEstimate is not null && flight.FeederFixEstimate is not null)
            {
                var diff = flight.FeederFixEstimate.Value - calculatedFeederFixEstimate.Value;
                flight.UpdateFeederFixEstimate(calculatedFeederFixEstimate.Value);
                logger.Debug(
                    "{Callsign} ETA_FF now {FeederFixEstimate} (diff {Difference})",
                    flight.Callsign,
                    flight.FeederFixEstimate,
                    diff.ToHoursAndMinutesString());
            }
        }

        var landingSystemEstimate = flight.Fixes.LastOrDefault();
        var calculatedLandingEstimate = estimateProvider.GetLandingEstimate(flight, landingSystemEstimate?.Estimate);
        if (calculatedLandingEstimate is not null)
        {
            var diff = flight.LandingEstimate - calculatedLandingEstimate.Value;
            flight.UpdateLandingEstimate(calculatedLandingEstimate.Value);
            logger.Debug(
                "{Callsign} ETA now {LandingEstimate} (diff {Difference})",
                flight.Callsign,
                flight.LandingEstimate,
                diff.ToHoursAndMinutesString());
        }
    }
}
