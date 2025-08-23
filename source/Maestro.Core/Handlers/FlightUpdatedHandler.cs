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
    DateTimeOffset EstimatedDepartureTime,
    string? AssignedArrival,
    string? AssignedRunway,
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

            var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
                .Single(a => a.Identifier == notification.Destination);

            var flight = sequence.FindTrackedFlight(notification.Callsign);
            if (flight is null)
            {
                // TODO: Make configurable
                var flightCreationThreshold = TimeSpan.FromHours(2);

                var feederFix = notification.Estimates.LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
                var landingEstimate = notification.Estimates.Last().Estimate;
                var hasDeparted = notification.Position is not null && !notification.Position.IsOnGround;

                // Flights are added to the pending list if they are departing from a configured departure airport
                if (airportConfiguration.DepartureAirports.Contains(notification.Origin) && !hasDeparted)
                {
                    flight = CreateMaestroFlight(
                        notification,
                        feederFix,
                        landingEstimate);

                    flight.SetState(State.Pending, clock);
                    sequence.AddFlight(flight, scheduler);

                    logger.Information("{Callsign} created (pending)", notification.Callsign);
                    return;
                }

                // TODO: Determine if this behaviour is correct
                if (!hasDeparted)
                    return;

                // Only create flights in Maestro when they're within a specified range of the feeder fix
                if (feederFix is not null && feederFix.Estimate - clock.UtcNow() <= flightCreationThreshold)
                {
                    flight = CreateMaestroFlight(
                        notification,
                        feederFix,
                        landingEstimate);

                    flight.SetState(State.New, clock);
                    sequence.AddFlight(flight, scheduler);
                    logger.Information("{Callsign} created", notification.Callsign);
                }
                // Flights not tracking a feeder fix are created with high priority
                else if (feederFix is null && landingEstimate - clock.UtcNow() <= flightCreationThreshold)
                {
                    flight = CreateMaestroFlight(
                        notification,
                        null,
                        landingEstimate);

                    flight.HighPriority = true;

                    flight.SetState(State.New, clock);
                    sequence.AddFlight(flight, scheduler);
                    logger.Information("{Callsign} created (high priority)", notification.Callsign);
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

            // TODO: Move this into the recompute handler
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
            notification.EstimatedDepartureTime,
            feederFixEstimate,
            landingEstimate);

        if (feederFixEstimate is null)
            flight.HighPriority = true;

        return flight;
    }
}
