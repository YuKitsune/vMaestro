using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RecomputeRequestHandler(
    ISequenceProvider sequenceProvider,
    IAirportConfigurationProvider airportConfigurationProvider,
    IEstimateProvider estimateProvider,
    IClock clock,
    IScheduler scheduler,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<RecomputeRequest, RecomputeResponse>
{
    public async Task<RecomputeResponse> Handle(RecomputeRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSequence.Sequence;

        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
            .Single(a => a.Identifier == request.AirportIdentifier);

        var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RecomputeResponse();
        }

        logger.Information("Recomputing {Callsign}", flight.Callsign);

        // Reset the feeder fix in case of a re-route
        var feederFix = flight.Fixes
            .LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
        if (feederFix is not null)
            flight.SetFeederFix(feederFix.FixIdentifier, feederFix.Estimate, feederFix.ActualTimeOver);

        flight.HighPriority = feederFix is null;
        flight.NoDelay = false;

        // Re-calculate the estimates
        CalculateEstimates(airportConfiguration, flight);
        flight.SetLandingTime(flight.EstimatedLandingTime, manual: false);

        // Reset the runway
        var runwayMode = sequence.GetRunwayModeAt(flight.EstimatedLandingTime);
        flight.SetRunway(runwayMode.Default.Identifier, manual: false);

        // Treat as a new flight
        flight.SetState(State.New, clock);

        // Re-schedule the sequence
        scheduler.Schedule(sequence);

        flight.UpdateStateBasedOnTime(clock);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                lockedSequence.Sequence.AirportIdentifier,
                lockedSequence.Sequence.ToMessage()),
            cancellationToken);

        return new RecomputeResponse();
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
            if (calculatedFeederFixEstimate is not null && flight.EstimatedFeederFixTime is not null)
            {
                var diff = flight.EstimatedFeederFixTime.Value - calculatedFeederFixEstimate.Value;
                flight.UpdateFeederFixEstimate(calculatedFeederFixEstimate.Value);
                logger.Debug(
                    "{Callsign} ETA_FF now {FeederFixEstimate} (diff {Difference})",
                    flight.Callsign,
                    flight.EstimatedFeederFixTime,
                    diff.ToHoursAndMinutesString());

                if (diff.Duration() > TimeSpan.FromMinutes(2))
                    logger.Warning("{Callsign} ETA_FF has changed by more than 2 minutes", flight.Callsign);
            }
        }

        var landingSystemEstimate = flight.Fixes.LastOrDefault();
        var calculatedLandingEstimate = estimateProvider.GetLandingEstimate(flight, landingSystemEstimate?.Estimate);
        if (calculatedLandingEstimate is not null)
        {
            var diff = flight.EstimatedLandingTime - calculatedLandingEstimate.Value;
            flight.UpdateLandingEstimate(calculatedLandingEstimate.Value);
            logger.Debug(
                "{Callsign} ETA now {LandingEstimate} (diff {Difference})",
                flight.Callsign,
                flight.EstimatedLandingTime,
                diff.ToHoursAndMinutesString());

            if (diff.Duration() > TimeSpan.FromMinutes(2))
                logger.Warning("{Callsign} ETA has changed by more than 2 minutes", flight.Callsign);
        }
    }
}
