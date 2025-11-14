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

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    AircraftCategory AircraftCategory,
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
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IFlightUpdateRateLimiter rateLimiter,
    IAirportConfigurationProvider airportConfigurationProvider,
    IEstimateProvider estimateProvider,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : INotificationHandler<FlightUpdatedNotification>
{
    public async Task Handle(FlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            if (!instanceManager.InstanceExists(notification.Destination))
                return;

            var instance = await instanceManager.GetInstance(notification.Destination, cancellationToken);
            SessionMessage sessionMessage;

            using (await instance.Semaphore.LockAsync(cancellationToken))
            {
                // Rate-limit updates for existing flights
                var flight = FindFlight(instance.Session, notification.Callsign);
                if (flight is not null)
                {
                    var shouldUpdate = rateLimiter.ShouldUpdateFlight(flight);
                    if (!shouldUpdate)
                    {
                        logger.Verbose("Rate limiting {Callsign}", notification.Callsign);
                        return;
                    }
                }

                if (connectionManager.TryGetConnection(notification.Destination, out var connection) &&
                     connection.IsConnected &&
                     !connection.IsMaster)
                {
                    logger.Debug("Relaying FlightUpdatedNotification for {Callsign}", notification.Callsign);
                    await connection.Send(notification, cancellationToken);
                    return;
                }

                var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
                    .Single(a => a.Identifier == notification.Destination);

                if (flight is null)
                {
                    // TODO: Make configurable
                    var flightCreationThreshold = TimeSpan.FromHours(2);

                    var feederFix = notification.Estimates.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
                    var landingEstimate = notification.Estimates.Last().Estimate;
                    var hasDeparted = notification.Position is not null && !notification.Position.IsOnGround;
                    var feederFixTimeIsNotKnown = feederFix is not null && feederFix.ActualTimeOver == DateTimeOffset.MaxValue; // vatSys uses MaxValue when the fix has been overflown, but the time is not known (i.e. controller connecting after the event)

                    // Flights are added to the pending list if they are departing from a configured departure airport
                    if (feederFixTimeIsNotKnown || (airportConfiguration.DepartureAirports.Contains(notification.Origin) && !hasDeparted))
                    {
                        flight = CreateMaestroFlight(
                            notification,
                            feederFix,
                            landingEstimate);

                        flight.IsFromDepartureAirport = true;

                        instance.Session.PendingFlights.Add(flight);

                        logger.Information("{Callsign} created (pending)", notification.Callsign);
                        await mediator.Publish(new CoordinationMessageSentNotification(
                            notification.Destination,
                            $"{notification.Callsign} added to pending list",
                            new CoordinationDestination.Broadcast()),
                            cancellationToken);

                        return;
                    }

                    // TODO: Determine if this behaviour is correct
                    if (!hasDeparted)
                        return;

                    // Only create flights in Maestro when they're within a specified range of the feeder fix
                    if (feederFix is not null && feederFix.Estimate - clock.UtcNow() <= flightCreationThreshold)
                    {
                        // Determine the runway to assign
                        var runwayMode = instance.Session.Sequence.GetRunwayModeAt(landingEstimate);
                        var runway = FindBestRunway(airportConfiguration, runwayMode, feederFix?.FixIdentifier ?? string.Empty);

                        // New flights can be inserted in front of existing Unstable and Stable flights on the same runway
                        var earliestInsertionIndex = instance.Session.Sequence.FindLastIndex(f =>
                            f.State is not State.Unstable and not State.Stable &&
                            f.AssignedRunwayIdentifier == runway.Identifier);

                        var insertionIndex = instance.Session.Sequence.FindIndex(
                            Math.Max(earliestInsertionIndex, 0),
                            f => f.LandingEstimate.IsBefore(landingEstimate)) + 1;

                        flight = CreateMaestroFlight(
                            notification,
                            feederFix,
                            landingEstimate);

                        flight.SetRunway(runway.Identifier, manual: false);

                        instance.Session.Sequence.Insert(insertionIndex, flight);
                        logger.Information("{Callsign} created", notification.Callsign);
                    }
                    // Flights not tracking a feeder fix are added to the pending list
                    else if (feederFix is null && landingEstimate - clock.UtcNow() <= flightCreationThreshold)
                    {
                        flight = CreateMaestroFlight(
                            notification,
                            null,
                            landingEstimate);

                        instance.Session.PendingFlights.Add(flight);
                        logger.Information("{Callsign} created (pending)", notification.Callsign);
                    }
                }

                if (flight is null)
                    return;

                flight.UpdateLastSeen(clock);

                UpdateFlightData(notification, flight);
                flight.UpdatePosition(notification.Position);

                // Only update the estimates if the flight is coupled to a radar track, and it's not on the ground
                if (notification.Position is not null && !notification.Position.IsOnGround)
                    CalculateEstimates(flight, notification, airportConfiguration);

                logger.Verbose("Flight updated: {Flight}", flight);

                // Unstable flights are repositioned in the sequence on every update
                if (flight.State is State.Unstable)
                    instance.Session.Sequence.RepositionByEstimate(flight);

                flight.UpdateStateBasedOnTime(clock);

                sessionMessage = instance.Session.Snapshot();
            }

            await mediator.Publish(
                new SessionUpdatedNotification(
                    instance.AirportIdentifier,
                    sessionMessage),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Error updating {Callsign}", notification.Callsign);
        }
    }

    Runway FindBestRunway(AirportConfiguration airportConfiguration, RunwayMode runwayMode, string feederFixIdentifier)
    {
        if (airportConfiguration.PreferredRunways.TryGetValue(feederFixIdentifier, out var preferredRunways))
        {
            return runwayMode.Runways
                       .FirstOrDefault(r => preferredRunways.Contains(r.Identifier))
                   ?? runwayMode.Default;
        }

        return runwayMode.Default;
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
        if ((feederFixSystemEstimate is not null && flight is { HasPassedFeederFix: false, ManualFeederFixEstimate: false }))
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
        var flight = new Flight(
            notification.Callsign,
            notification.Destination,
            landingEstimate,
            clock.UtcNow(),
            notification.AircraftType,
            notification.AircraftCategory,
            notification.WakeCategory);

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
        // TODO: Figure out what needs to be updated only on recompute
        flight.AircraftType = notification.AircraftType;
        flight.AircraftCategory = notification.AircraftCategory;
        flight.WakeCategory = notification.WakeCategory;

        flight.OriginIdentifier = notification.Origin;
        flight.EstimatedDepartureTime = notification.EstimatedDepartureTime;
        flight.EstimatedTimeEnroute = notification.EstimatedFlightTime;

        flight.AssignedArrivalIdentifier = notification.AssignedArrival;
        flight.Fixes = notification.Estimates;
    }

    Flight? FindFlight(Session session, string callsign)
    {
        return session.Sequence.FindFlight(callsign) ??
               session.PendingFlights.SingleOrDefault(f => f.Callsign == callsign) ??
               session.DeSequencedFlights.SingleOrDefault(f => f.Callsign == callsign);
    }
}
