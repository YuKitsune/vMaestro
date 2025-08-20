using System.Security.Authentication.ExtendedProtection;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    WakeCategory WakeCategory,
    string Origin,
    string Destination,
    string? AssignedArrival,
    string? AssignedRunway,
    bool Activated,
    FlightPosition? Position,
    FixEstimate[] Estimates)
    : INotification;

public class FlightUpdatedHandler(
    ISequenceProvider sequenceProvider,
    IRunwayAssigner runwayAssigner,
    IFlightUpdateRateLimiter rateLimiter,
    IAirportConfigurationProvider airportConfigurationProvider,
    IEstimateProvider estimateProvider,
    IScheduler scheduler,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : INotificationHandler<FlightUpdatedNotification>
{
    public async Task Handle(FlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            if (!sequenceProvider.CanSequenceFor(notification.Destination))
                return;

            logger.Verbose("Received update for {Callsign}", notification.Callsign);

            using var lockedSequence = await sequenceProvider.GetSequence(notification.Destination, cancellationToken);
            var sequence = lockedSequence.Sequence;

            var isNew = false;
            var flight = sequence.FindTrackedFlight(notification.Callsign);
            if (flight is null)
            {
                isNew = true;

                // TODO: Make configurable
                var flightCreationThreshold = TimeSpan.FromHours(2);

                var feederFix = notification.Estimates
                    .LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
                var landingEstimate = notification.Estimates.Last().Estimate;

                // TODO: Verify if this behaviour is correct
                // Flights not planned via a feeder fix are added to the pending list
                if (feederFix is null)
                {
                    flight = CreateMaestroFlight(
                        notification,
                        null,
                        landingEstimate);

                    // TODO: Revisit flight plan activation
                    flight.Activate(clock);

                    sequence.AddPendingFlight(flight);
                    logger.Information("{Callsign} created (pending)", notification.Callsign);
                }
                // Only create flights in Maestro when they're within a specified range of the feeder fix
                else if (feederFix.Estimate - clock.UtcNow() <= flightCreationThreshold)
                {
                    flight = CreateMaestroFlight(
                        notification,
                        feederFix,
                        landingEstimate);

                    // TODO: Revisit flight plan activation
                    flight.Activate(clock);

                    sequence.AddFlight(flight, scheduler);
                    logger.Information("{Callsign} created", notification.Callsign);
                }
            }

            if (flight is null)
                return;

            // Only apply rate limiting if a position is available
            // When no position is available (i.e. Not coupled to a radar track), we accept all updates
            if (notification.Position is not null)
            {
                var shouldUpdate = rateLimiter.ShouldUpdateFlight(flight, notification.Position);
                if (!shouldUpdate)
                {
                    logger.Verbose("Rate limiting {Callsign}", notification.Callsign);
                    return;
                }
            }

            logger.Debug("Updating {Callsign}", notification.Callsign);

            flight.UpdateLastSeen(clock);
            flight.SetArrival(notification.AssignedArrival);

            // TODO: Revisit flight plan activation
            // The flight becomes 'active' in Maestro when the flight is activated in TAAATS.
            // It is then updated by regular reports from the TAAATS FDP to the Maestro System.
            // if (!flight.Activated && notification.Activated)
            // {
            //     flight.Activate(clock);
            // }

            // Exit early if the flight should not be processed
            if (!flight.Activated ||
                flight.State == State.Desequenced ||
                flight.State == State.Removed ||
                flight.State == State.Landed)
            {
                if (!flight.Activated)
                    logger.Debug("{Callsign} is not activated. No additional processing required.",
                        notification.Callsign);
                else
                    logger.Debug("{Callsign} is {State}. No additional processing required.", notification.Callsign,
                        flight.State);

                return;
            }

            // TODO: Move this into the scheduler
            if (flight.NeedsRecompute)
            {
                logger.Information("Recomputing {Callsign}", flight.Callsign);

                // Reset the feeder fix in case of a reroute
                var feederFix = notification.Estimates
                    .LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
                if (feederFix is not null)
                    flight.SetFeederFix(feederFix.FixIdentifier, feederFix.Estimate, feederFix.ActualTimeOver);

                flight.HighPriority = feederFix is null;
                flight.NoDelay = false;// Re-assign runway if it has not been manually assigned

                if (!flight.RunwayManuallyAssigned)
                {
                    flight.ClearRunway();
                }

                scheduler.Schedule(sequence);

                flight.NeedsRecompute = false;
            }

            // Compute ETA and ETA_FF
            var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations().Single(a => a.Identifier == flight.DestinationIdentifier);
            CalculateEstimates(flight, notification, airportConfiguration);

            logger.Debug("Flight updated: {Flight}", flight);

            await mediator.Publish(
                new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Error updating {Callsign}", notification.Callsign);
        }
    }

    void CalculateEstimates(Flight flight, FlightUpdatedNotification notification, AirportConfiguration airportConfiguration)
    {
        var feederFixSystemEstimate = notification.Estimates.LastOrDefault(e => e.FixIdentifier == flight.FeederFixIdentifier);
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
                notification.Position);
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

        var landingSystemEstimate = notification.Estimates.LastOrDefault();
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

    Flight CreateMaestroFlight(
        FlightUpdatedNotification notification,
        FixEstimate? feederFixEstimate,
        DateTimeOffset landingEstimate)
    {
        var flight = new Flight(
            notification.Callsign,
            notification.AircraftType,
            notification.WakeCategory,
            notification.Origin,
            notification.Destination,
            feederFixEstimate,
            landingEstimate);

        if (feederFixEstimate is null)
            flight.HighPriority = true;

        return flight;
    }
}
