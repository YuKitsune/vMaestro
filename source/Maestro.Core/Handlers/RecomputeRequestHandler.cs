using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RecomputeRequestHandler(
    ISessionManager sessionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IEstimateProvider estimateProvider,
    IClock clock,
    IScheduler scheduler,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<RecomputeRequest>
{
    public async Task Handle(RecomputeRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Send(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
            .Single(a => a.Identifier == request.AirportIdentifier);

        var flight = sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return;
        }

        logger.Information("Recomputing {Callsign}", flight.Callsign);

        // Reset the feeder fix in case of a re-route
        var feederFix = flight.Fixes.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
        if (feederFix is not null)
            flight.SetFeederFix(feederFix.FixIdentifier, feederFix.Estimate, feederFix.ActualTimeOver);

        flight.HighPriority = feederFix is null;
        flight.NoDelay = false;

        // Re-calculate the estimates
        CalculateEstimates(airportConfiguration, flight);
        flight.ResetInitialLandingEstimate();
        if (feederFix is not null)
            flight.ResetInitialFeederFixEstimate();

        // Reset scheduled times
        if (flight.FeederFixEstimate is not null)
            flight.SetFeederFixTime(flight.FeederFixEstimate.Value);

        flight.SetLandingTime(flight.LandingEstimate, manual: false);

        // Reset the runway
        var runwayMode = sequence.GetRunwayModeAt(flight.LandingEstimate);
        flight.SetRunway(runwayMode.Default.Identifier, manual: false);

        scheduler.Recompute(flight, sequence);

        // Progress the state based on the new times
        flight.UpdateStateBasedOnTime(clock);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
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

                if (diff.Duration() > TimeSpan.FromMinutes(2))
                    logger.Warning("{Callsign} ETA_FF has changed by more than 2 minutes", flight.Callsign);
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

            if (diff.Duration() > TimeSpan.FromMinutes(2))
                logger.Warning("{Callsign} ETA has changed by more than 2 minutes", flight.Callsign);
        }
    }
}
