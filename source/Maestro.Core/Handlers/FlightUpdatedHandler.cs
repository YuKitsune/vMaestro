using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
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
    TimeSpan EstimatedFlightTime,
    string? AssignedArrival,
    FlightPosition? Position,
    FixEstimate[] Estimates)
    : INotification;

public class FlightUpdatedHandler(
    ISessionManager sessionManager,
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
            if (!sessionManager.HasSessionFor(notification.Destination))
                return;

            using var lockedSession = await sessionManager.AcquireSession(notification.Destination, cancellationToken);
            if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
            {
                await lockedSession.Session.Connection.Send(notification, cancellationToken);
                return;
            }

            var sequence = lockedSession.Session.Sequence;
            logger.Debug("Received update for {Callsign}", notification.Callsign);

            var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
                .Single(a => a.Identifier == notification.Destination);

            var flight = sequence.FindTrackedFlight(notification.Callsign);
            if (flight is null)
            {
                // TODO: Make configurable
                var flightCreationThreshold = TimeSpan.FromHours(2);

                var feederFix = notification.Estimates.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
                var landingEstimate = notification.Estimates.Last().Estimate;
                var hasDeparted = notification.Position is not null && !notification.Position.IsOnGround;

                // Flights are added to the pending list if they are departing from a configured departure airport
                if (airportConfiguration.DepartureAirports.Contains(notification.Origin) && !hasDeparted)
                {
                    flight = CreateMaestroFlight(
                        notification,
                        feederFix,
                        landingEstimate);

                    flight.IsFromDepartureAirport = true;
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

            UpdateFlightData(notification, flight);
            flight.UpdatePosition(notification.Position);

            // Only update the estimates if the flight is coupled to a radar track, and it's not on the ground
            if (notification.Position is not null && !notification.Position.IsOnGround)
                    CalculateEstimates(flight, notification, airportConfiguration);

            flight.UpdateLastSeen(clock);

            logger.Verbose("Flight updated: {Flight}", flight);

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

        // Don't update ETA_FF once passed FF, or if a manual estimate is set
        if ((feederFixSystemEstimate is not null && !flight.HasPassedFeederFix) || flight.ManualFeederFixEstimate)
        {
            var calculatedFeederFixEstimate = estimateProvider.GetFeederFixEstimate(
                airportConfiguration,
                flight.FeederFixIdentifier!,
                feederFixSystemEstimate!.Estimate,
                notification.Position);
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

        var landingSystemEstimate = notification.Estimates.LastOrDefault();
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

    Flight CreateMaestroFlight(
        FlightUpdatedNotification notification,
        FixEstimate? feederFixEstimate,
        DateTimeOffset landingEstimate)
    {
        var flight = new Flight(notification.Callsign, notification.Destination, landingEstimate);
        UpdateFlightData(notification, flight);

        if (feederFixEstimate is not null)
            flight.SetFeederFix(feederFixEstimate.FixIdentifier, feederFixEstimate.Estimate, feederFixEstimate.ActualTimeOver);

        flight.UpdateLandingEstimate(landingEstimate);
        flight.ResetInitialEstimates();

        if (feederFixEstimate is null)
            flight.HighPriority = true;

        return flight;
    }

    void UpdateFlightData(FlightUpdatedNotification notification, Flight flight)
    {
        flight.AircraftType = notification.AircraftType;
        flight.WakeCategory = notification.WakeCategory;

        flight.OriginIdentifier = notification.Origin;
        flight.EstimatedDepartureTime = notification.EstimatedDepartureTime;
        flight.EstimatedTimeEnroute = notification.EstimatedFlightTime;

        flight.AssignedArrivalIdentifier = notification.AssignedArrival;
        flight.Fixes = notification.Estimates;
    }
}
