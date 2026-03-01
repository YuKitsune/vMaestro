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
    FlightPosition? Position,
    FixEstimate[] Estimates)
    : INotification;

public class FlightUpdatedHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IFlightUpdateRateLimiter rateLimiter,
    IAirportConfigurationProvider airportConfigurationProvider,
    IArrivalLookup arrivalLookup,
    ITrajectoryService trajectoryService,
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

            logger.Verbose("FDR update received for {Callsign}", notification.Callsign);

            var instance = await instanceManager.GetInstance(notification.Destination, cancellationToken);
            SessionMessage sessionMessage;

            using (await instance.Semaphore.LockAsync(cancellationToken))
            {
                // Check each list individually to find the flight
                var sequencedFlight = instance.Session.Sequence.FindFlight(notification.Callsign);
                var pendingFlight = instance.Session.PendingFlights.SingleOrDefault(f => f.Callsign == notification.Callsign);
                var desequencedFlight = instance.Session.DeSequencedFlights.SingleOrDefault(f => f.Callsign == notification.Callsign);

                var existingFlight = sequencedFlight ?? pendingFlight ?? desequencedFlight;

                // Rate-limit updates for existing flights
                if (existingFlight is not null)
                {
                    var shouldUpdate = rateLimiter.ShouldUpdateFlight(existingFlight);
                    if (!shouldUpdate)
                    {
                        logger.Verbose("FDR update for {Callsign} rate-limited", notification.Callsign);
                        return;
                    }
                }

                if (connectionManager.TryGetConnection(notification.Destination, out var connection) &&
                     connection.IsConnected &&
                     !connection.IsMaster)
                {
                    logger.Verbose("Relaying FlightUpdatedNotification for {Callsign}", notification.Callsign);
                    await connection.Send(notification, cancellationToken);
                    return;
                }

                var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
                    .Single(a => a.Identifier == notification.Destination);

                if (existingFlight is null)
                {
                    // TODO: Make configurable
                    var flightCreationThreshold = TimeSpan.FromHours(2);

                    var feederFix = notification.Estimates.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
                    var approximateLandingEstimate = notification.Estimates.Last().Estimate;
                    var hasDeparted = notification.Position is not null && !notification.Position.IsOnGround;

                    // vatSys uses MaxValue when the fix has been overflown, but the time is not known (i.e. controller connecting after the event)
                    var feederFixTimeIsNotKnown = feederFix is not null && feederFix.ActualTimeOver == DateTimeOffset.MaxValue;

                    // Flights are added to the pending list if they are departing from a configured departure airport
                    if (feederFixTimeIsNotKnown || (airportConfiguration.DepartureAirports.Any(d => d.Identifier == notification.Origin) && !hasDeparted))
                    {
                        // For pending flights, use the default runway to calculate trajectory
                        var runwayMode = instance.Session.Sequence.GetRunwayModeAt(approximateLandingEstimate);
                        var runway = runwayMode.Default;

                        var trajectory = trajectoryService.GetTrajectory(
                            notification.AircraftType,
                            notification.AircraftCategory,
                            notification.Destination,
                            feederFix?.FixIdentifier,
                            runway.Identifier,
                            runway.ApproachType);

                        var newPendingFlight = new Flight(
                            callsign: notification.Callsign,
                            aircraftType: notification.AircraftType,
                            aircraftCategory: notification.AircraftCategory,
                            wakeCategory: notification.WakeCategory,
                            destinationIdentifier: notification.Destination,
                            originIdentifier: notification.Origin,
                            isFromDepartureAirport: true,
                            estimatedDepartureTime: notification.EstimatedDepartureTime,
                            assignedRunwayIdentifier: runway.Identifier,
                            runway.ApproachType,
                            trajectory: trajectory,
                            feederFixIdentifier: feederFix?.FixIdentifier,
                            feederFixEstimate: feederFix?.Estimate,
                            landingEstimate: approximateLandingEstimate,
                            activatedTime: clock.UtcNow(),
                            fixes: notification.Estimates,
                            position: notification.Position);

                        instance.Session.PendingFlights.Add(newPendingFlight);

                        logger.Information("Added {Callsign} to the Pending list (departing from a Departure airport)", notification.Callsign);

                        await mediator.Send(new SendCoordinationMessageRequest(
                            notification.Destination,
                            clock.UtcNow(),
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
                        // Use the default runway for now. This will get re-calculated in the scheduling phase.
                        var runwayMode = instance.Session.Sequence.GetRunwayModeAt(approximateLandingEstimate);
                        var runway = runwayMode.Default;

                        var trajectory = trajectoryService.GetTrajectory(
                            notification.AircraftType,
                            notification.AircraftCategory,
                            notification.Destination,
                            feederFix.FixIdentifier,
                            runway.Identifier,
                            runway.ApproachType);

                        // New flights can be inserted in front of existing Unstable and Stable flights on the same runway
                        var earliestInsertionIndex = instance.Session.Sequence.FindLastIndex(f =>
                            f.State is not State.Unstable and not State.Stable &&
                            f.AssignedRunwayIdentifier == runway.Identifier) + 1;

                        var insertionIndex = instance.Session.Sequence.FindIndex(
                            earliestInsertionIndex,
                            f => f.FeederFixEstimate.IsAfter(feederFix.Estimate));
                        if (insertionIndex == -1)
                            insertionIndex = Math.Min(earliestInsertionIndex, instance.Session.Sequence.Flights.Count);

                        sequencedFlight = new Flight(
                            callsign: notification.Callsign,
                            aircraftType: notification.AircraftType,
                            aircraftCategory: notification.AircraftCategory,
                            wakeCategory: notification.WakeCategory,
                            destinationIdentifier: notification.Destination,
                            originIdentifier: notification.Origin,
                            isFromDepartureAirport: false,
                            estimatedDepartureTime: notification.EstimatedDepartureTime,
                            assignedRunwayIdentifier: runway.Identifier,
                            approachType: runway.ApproachType,
                            trajectory: trajectory,
                            feederFixIdentifier: feederFix.FixIdentifier,
                            feederFixEstimate: feederFix.Estimate,
                            landingEstimate: approximateLandingEstimate,
                            activatedTime: clock.UtcNow(),
                            fixes: notification.Estimates,
                            position: notification.Position);

                        instance.Session.Sequence.Insert(insertionIndex, sequencedFlight);
                        logger.Information("{Callsign} added to the sequence", notification.Callsign);
                    }
                    // Flights not tracking a feeder fix are added to the pending list
                    else if (feederFix is null && approximateLandingEstimate - clock.UtcNow() <= flightCreationThreshold)
                    {
                        // For pending flights without feeder fix, use the default runway
                        var runwayMode = instance.Session.Sequence.GetRunwayModeAt(approximateLandingEstimate);
                        var runway = runwayMode.Default;

                        var trajectory = trajectoryService.GetTrajectory(
                            notification.AircraftType,
                            notification.AircraftCategory,
                            notification.Destination,
                            null,
                            runway.Identifier,
                            runway.ApproachType);

                        pendingFlight = new Flight(
                            callsign: notification.Callsign,
                            aircraftType: notification.AircraftType,
                            aircraftCategory: notification.AircraftCategory,
                            wakeCategory: notification.WakeCategory,
                            destinationIdentifier: notification.Destination,
                            originIdentifier: notification.Origin,
                            isFromDepartureAirport: false,
                            estimatedDepartureTime: notification.EstimatedDepartureTime,
                            assignedRunwayIdentifier: runway.Identifier,
                            approachType: runway.ApproachType,
                            trajectory: trajectory,
                            feederFixIdentifier: null,
                            feederFixEstimate: null,
                            landingEstimate: approximateLandingEstimate,
                            activatedTime: clock.UtcNow(),
                            fixes: notification.Estimates,
                            position: notification.Position);

                        pendingFlight.HighPriority = true;

                        instance.Session.PendingFlights.Add(pendingFlight);

                        await mediator.Send(
                            new SendCoordinationMessageRequest(
                                notification.Destination,
                                clock.UtcNow(),
                                $"{notification.Callsign} added to pending list",
                                new CoordinationDestination.Broadcast()),
                            cancellationToken);

                        logger.Information("{Callsign} added to the Pending list (no matching FF)", notification.Callsign);
                    }
                }

                // Handle pending flights: only update flight data
                if (pendingFlight is not null)
                {
                    pendingFlight.UpdateLastSeen(clock);
                    UpdateFlightData(notification, pendingFlight);

                    pendingFlight.UpdatePosition(notification.Position);
                    logger.Verbose("Pending flight updated: {Flight}", pendingFlight);
                }
                // Handle desequenced flights: update flight data and calculate estimates
                else if (desequencedFlight is not null)
                {
                    desequencedFlight.UpdateLastSeen(clock);
                    UpdateFlightData(notification, desequencedFlight);

                    desequencedFlight.UpdatePosition(notification.Position);

                    // Only update the estimates if the flight is coupled to a radar track, and it's not on the ground
                    if (notification.Position is not null && !notification.Position.IsOnGround)
                        CalculateEstimates(desequencedFlight, notification);

                    desequencedFlight.UpdateStateBasedOnTime(clock);
                    logger.Verbose("Desequenced flight updated: {Flight}", desequencedFlight);
                }
                // Handle sequenced flights: update flight data, calculate estimates, and reposition if unstable
                else if (sequencedFlight is not null)
                {
                    sequencedFlight.UpdateLastSeen(clock);
                    UpdateFlightData(notification, sequencedFlight);

                    sequencedFlight.UpdatePosition(notification.Position);

                    // Only update the estimates if the flight is coupled to a radar track, and it's not on the ground
                    if (notification.Position is not null && !notification.Position.IsOnGround)
                        CalculateEstimates(sequencedFlight, notification);

                    logger.Verbose("Flight updated: {Flight}", sequencedFlight);

                    // Unstable flights are repositioned in the sequence on every update
                    if (sequencedFlight.State is State.Unstable)
                    {
                        // Do not overtake any stable flights
                        var currentIndex = instance.Session.Sequence.IndexOf(sequencedFlight);
                        var earliestIndex = instance.Session.Sequence.FindLastIndex(
                            currentIndex,
                            f => f.AssignedRunwayIdentifier == sequencedFlight.AssignedRunwayIdentifier &&
                                 f.State != State.Unstable) + 1;

                        var desiredIndex = instance.Session.Sequence.FindIndex(f =>
                            f.FeederFixEstimate.IsAfter(sequencedFlight.FeederFixEstimate));

                        var newIndex = desiredIndex == -1
                            ? instance.Session.Sequence.Flights.Count // No flight has a later estimate - move to end of sequence
                            : desiredIndex;

                        // Cannot move before stable flights that are currently before us
                        if (newIndex < earliestIndex)
                            newIndex = earliestIndex;

                        if (newIndex != currentIndex)
                        {
                            sequencedFlight.InvalidateSequenceData();
                            instance.Session.Sequence.Move(sequencedFlight, newIndex);
                        }
                    }

                    sequencedFlight.UpdateStateBasedOnTime(clock);
                }

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

    void CalculateEstimates(Flight flight, FlightUpdatedNotification notification)
    {
        // Stop re-calculating estimates after passing the feeder
        if (flight.HasPassedFeederFix || flight.ManualFeederFixEstimate)
            return;

        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier))
        {
            var feederFixSystemEstimate = notification.Estimates.LastOrDefault(e => e.FixIdentifier == flight.FeederFixIdentifier);
            if (feederFixSystemEstimate is not null)
            {
                if (feederFixSystemEstimate.ActualTimeOver.HasValue)
                {
                    logger.Information(
                        "{Callsign} passed {FeederFix} at {ActualTimeOver}",
                        flight.Callsign,
                        flight.FeederFixIdentifier,
                        feederFixSystemEstimate.ActualTimeOver);

                    flight.PassedFeederFix(feederFixSystemEstimate.ActualTimeOver.Value);
                }
                else
                {
                    logger.Verbose(
                        "{Callsign} ETA_FF for {FeederFix} now {FeederFixEstimate}",
                        flight.Callsign,
                        flight.FeederFixIdentifier,
                        feederFixSystemEstimate.Estimate);

                    flight.UpdateFeederFixEstimate(feederFixSystemEstimate.Estimate);
                }

                return;
            }
        }

        // Feeder fix estimate couldn't be determined from the route, calculate it using ETA - TTG
        var landingEstimate = notification.Estimates.LastOrDefault();
        if (landingEstimate is null)
            throw new MaestroException($"Couldn't determine estimate for {flight.Callsign}");

        var now = clock.UtcNow();
        var feederFixEstimate = landingEstimate.Estimate.Subtract(flight.Trajectory.TimeToGo);
        if (feederFixEstimate.IsSameOrBefore(now))
        {
            logger.Information(
                "{Callsign} (no FF) entered the TMA at approximately {ActualTimeOver}",
                flight.Callsign,
                feederFixEstimate);

            flight.PassedFeederFix(feederFixEstimate);
        }
        else
        {
            logger.Verbose(
                "{Callsign} (no FF) ETA_FF approximately {FeederFixEstimate}",
                flight.Callsign,
                feederFixEstimate);

            flight.UpdateFeederFixEstimate(feederFixEstimate);
        }
    }

    void UpdateFlightData(FlightUpdatedNotification notification, Flight flight)
    {
        flight.AircraftType = notification.AircraftType;
        flight.AircraftCategory = notification.AircraftCategory;
        flight.WakeCategory = notification.WakeCategory;

        flight.OriginIdentifier = notification.Origin;
        flight.EstimatedDepartureTime = notification.EstimatedDepartureTime;

        flight.Fixes = notification.Estimates;
    }
}
