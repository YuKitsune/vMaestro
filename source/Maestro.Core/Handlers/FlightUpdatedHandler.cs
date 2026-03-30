using Maestro.Contracts.Coordination;
using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class FlightUpdatedHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IFlightUpdateRateLimiter rateLimiter,
    IAirportConfigurationProvider airportConfigurationProvider,
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

            logger.Debug("FDR update received for {Callsign}", notification.Callsign);

            var instance = await instanceManager.GetInstance(notification.Destination, cancellationToken);
            SessionDto sessionDto;

            using (await instance.Semaphore.LockAsync(cancellationToken))
            {
                // Check each list individually to find the flight
                var sequencedFlight = instance.Session.Sequence.FindFlight(notification.Callsign);
                var pendingFlight = instance.Session.PendingFlights.SingleOrDefault(f => f.Callsign == notification.Callsign);
                var desequencedFlight = instance.Session.DeSequencedFlights.SingleOrDefault(f => f.Callsign == notification.Callsign);

                var isKnownFlight = sequencedFlight is not null || pendingFlight is not null || desequencedFlight is not null;

                // Rate-limit updates for known flights using the last seen time from the store
                if (isKnownFlight &&
                    instance.Session.FlightDataRecords.TryGetValue(notification.Callsign, out var existingData))
                {
                    if (!rateLimiter.ShouldUpdate(existingData.LastSeen))
                    {
                        logger.Debug("FDR update for {Callsign} rate-limited", notification.Callsign);
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

                // Always update the flight data store with the latest data
                var flightData = new FlightDataRecord(
                    notification.Callsign,
                    notification.AircraftType,
                    notification.AircraftCategory,
                    notification.WakeCategory,
                    notification.Origin,
                    notification.Destination,
                    notification.EstimatedDepartureTime,
                    notification.Position,
                    notification.Estimates,
                    clock.UtcNow());
                instance.Session.FlightDataRecords[notification.Callsign] = flightData;

                var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(notification.Destination);
                if (!isKnownFlight)
                {
                    var isFromDepartureAirport = airportConfiguration.DepartureAirports.Any(d => d.Identifier == notification.Origin);
                    var hasDeparted = notification.Position is not null && !notification.Position.IsOnGround;
                    var feederFix = notification.Estimates.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
                    var feederFixEstimateIsKnown = feederFix?.Estimate != null;
                    var approximateLandingEstimate = notification.Estimates.LastOrDefault()?.Estimate;

                    // Flights go to the pending list if they cannot be auto-inserted into the sequence:
                    // - Departure airport flights that haven't yet departed
                    // - Flights with no matching feeder fix
                    // - Flights with a feeder fix but no known estimate
                    // - Flights with no landing estimate at all
                    var addToPending = (isFromDepartureAirport && !hasDeparted)
                        || feederFix is null
                        || !feederFixEstimateIsKnown
                        || approximateLandingEstimate is null;

                    if (addToPending)
                    {
                        var newPendingFlight = new PendingFlight(
                            notification.Callsign,
                            IsFromDepartureAirport: isFromDepartureAirport,
                            IsHighPriority: feederFix is null);

                        instance.Session.PendingFlights.Add(newPendingFlight);

                        logger.Information("Added {Callsign} to the pending list", notification.Callsign);

                        await mediator.Send(new SendCoordinationMessageRequest(
                            notification.Destination,
                            clock.UtcNow(),
                            $"{notification.Callsign} added to pending list",
                            new CoordinationDestination.Broadcast()),
                            cancellationToken);

                        return;
                    }

                    // A flight is only added to the sequence once it is correlated to a radar track and airborne
                    if (!hasDeparted)
                        return;

                    // Only insert into the sequence once the feeder fix estimate is within the creation threshold
                    var flightCreationThreshold = TimeSpan.FromMinutes(airportConfiguration.FlightCreationThresholdMinutes);

                    // Safe to unwrap as we exit early when adding flights to the Pending list
                    // If the feederFix is null, the flight should be added to the Pending list
                    if (feederFix!.Estimate - clock.UtcNow() > flightCreationThreshold)
                        return;

                    // Use the default runway for now. This will get re-calculated in the scheduling phase.
                    var runwayMode = instance.Session.Sequence.GetRunwayModeAt(approximateLandingEstimate!.Value);
                    var runway = runwayMode.Default;

                    var enrouteTrajectory = trajectoryService.GetEnrouteTrajectory(
                        notification.Destination,
                        notification.Estimates.Select(e => e.FixIdentifier).ToArray(),
                        feederFix!.FixIdentifier);

                    var terminalTrajectory = trajectoryService.GetTrajectory(
                        new AircraftPerformanceData(notification.AircraftType, notification.AircraftCategory, notification.WakeCategory),
                        notification.Destination,
                        feederFix.FixIdentifier,
                        runway.Identifier,
                        runway.ApproachType,
                        notification.Estimates.Select(e => e.FixIdentifier).ToArray(),
                        instance.Session.Sequence.UpperWind);

                    // New flights can be inserted in front of existing Unstable and Stable flights on the same runway
                    var earliestInsertionIndex = instance.Session.Sequence.FindLastIndex(f =>
                        f.State is not State.Unstable and not State.Stable &&
                        f.AssignedRunwayIdentifier == runway.Identifier) + 1;

                    var insertionIndex = instance.Session.Sequence.FindIndex(
                        earliestInsertionIndex,
                        f => f.LandingEstimate.IsAfter(approximateLandingEstimate.Value));

                    if (insertionIndex == -1)
                        insertionIndex = Math.Min(earliestInsertionIndex, instance.Session.Sequence.Flights.Count);

                    sequencedFlight = new Flight(
                        callsign: notification.Callsign,
                        aircraftType: notification.AircraftType,
                        aircraftCategory: notification.AircraftCategory,
                        wakeCategory: notification.WakeCategory,
                        destinationIdentifier: notification.Destination,
                        originIdentifier: notification.Origin,
                        isFromDepartureAirport: isFromDepartureAirport,
                        estimatedDepartureTime: notification.EstimatedDepartureTime,
                        assignedRunwayIdentifier: runway.Identifier,
                        approachType: runway.ApproachType,
                        enrouteTrajectory: enrouteTrajectory,
                        terminalTrajectory: terminalTrajectory,
                        feederFixIdentifier: feederFix.FixIdentifier,
                        feederFixEstimate: feederFix.Estimate,
                        landingEstimate: approximateLandingEstimate.Value,
                        activatedTime: clock.UtcNow(),
                        position: notification.Position);

                    instance.Session.Sequence.Insert(insertionIndex, sequencedFlight);
                    logger.Verbose(
                        "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
                        notification.Callsign,
                        runway.Identifier,
                        runway.ApproachType,
                        trajectory.TimeToGo,
                        trajectory.Pressure,
                        trajectory.MaxPressure);
                    logger.Information("{Callsign} added to the sequence", notification.Callsign);
                    return;
                }

                // Handle pending flights: data is already updated in the store, nothing else to do
                if (pendingFlight is not null)
                {
                    logger.Debug("Pending flight data updated: {Callsign}", pendingFlight.Callsign);
                }
                // Handle desequenced flights: update flight data and calculate estimates
                else if (desequencedFlight is not null)
                {
                    desequencedFlight.UpdateLastSeen(clock);
                    UpdateFlightData(notification, desequencedFlight);
                    CalculateEstimates(desequencedFlight, notification);

                    desequencedFlight.UpdateStateBasedOnTime(clock, airportConfiguration);
                    logger.Debug("Desequenced flight updated: {Flight}", desequencedFlight);
                }
                // Handle sequenced flights: update flight data, calculate estimates, and reposition if unstable
                else if (sequencedFlight is not null)
                {
                    sequencedFlight.UpdateLastSeen(clock);
                    UpdateFlightData(notification, sequencedFlight);

                    // Recompute trajectory for unstable flights so wind changes propagate to TTG before estimates are calculated
                    if (sequencedFlight.State is State.Unstable && !string.IsNullOrEmpty(sequencedFlight.AssignedRunwayIdentifier))
                    {
                        var updatedTrajectory = trajectoryService.GetTrajectory(
                            sequencedFlight,
                            sequencedFlight.AssignedRunwayIdentifier,
                            sequencedFlight.ApproachType,
                            notification.Estimates.Select(e => e.FixIdentifier).ToArray(),
                            instance.Session.Sequence.UpperWind);
                        sequencedFlight.SetTrajectory(updatedTrajectory);
                        logger.Verbose(
                            "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
                            sequencedFlight.Callsign,
                            sequencedFlight.AssignedRunwayIdentifier,
                            sequencedFlight.ApproachType,
                            updatedTrajectory.TimeToGo,
                            updatedTrajectory.Pressure,
                            updatedTrajectory.MaxPressure);
                    }

                    // Only update the estimates if the flight is coupled to a radar track, and it's not on the ground
                    if (notification.Position is not null && !notification.Position.IsOnGround)
                        CalculateEstimates(sequencedFlight, notification);

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
                            f.LandingEstimate.IsAfter(sequencedFlight.LandingEstimate));

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

                    // Update the remaining delay distribution to reflect how much delay remains
                    var remainingDelay = sequencedFlight.LandingTime - sequencedFlight.LandingEstimate;
                    var remainingDistribution = DelayStrategyCalculator.Compute(
                        remainingDelay,
                        sequencedFlight.TerminalTrajectory,
                        sequencedFlight.EnrouteTrajectory,
                        airportConfiguration.DelayStrategy);
                    sequencedFlight.SetRemainingDelayData(remainingDistribution);

                    sequencedFlight.UpdateStateBasedOnTime(clock, airportConfiguration);

                    logger.Verbose("Flight updated: {Flight}", sequencedFlight);
                }

                sessionDto = instance.Session.Snapshot();
            }

            await mediator.Publish(
                new SessionUpdatedNotification(
                    instance.AirportIdentifier,
                    sessionDto),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Error updating {Callsign}", notification.Callsign);
        }
    }

    void CalculateEstimates(Flight flight, FlightUpdatedNotification notification)
    {
        if (flight.ManualFeederFixEstimate)
            return;

        // Only update the estimates if the flight is coupled to a radar track, and it's not on the ground
        if (flight.Position is null || flight.Position.IsOnGround)
            return;

        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier))
        {
            // If the feeder fix estimate is already in the past, the flight has crossed the FF; don't update further
            if (flight.FeederFixEstimate <= clock.UtcNow())
                return;

            var feederFixSystemEstimate = notification.Estimates.LastOrDefault(e => e.FixIdentifier == flight.FeederFixIdentifier);
            if (feederFixSystemEstimate?.Estimate != null)
            {
                logger.Debug(
                    "{Callsign} ETA_FF for {FeederFix} now {FeederFixEstimate}",
                    flight.Callsign,
                    flight.FeederFixIdentifier,
                    feederFixSystemEstimate.Estimate);

                flight.UpdateFeederFixEstimate(feederFixSystemEstimate.Estimate.Value);
                // Landing estimate automatically calculated using TTG
            }

            // If they have a feeder fix set but no estimate, they've probably passed it, so leave the estimates as-is
            return;
        }

        // No feeder fix estimate available
        // Update landing estimate directly, ETA_FF will be calculated using TTG
        var landingEstimate = notification.Estimates.LastOrDefault()?.Estimate;
        if (landingEstimate is null)
        {
            logger.Warning(
                "No estimates available for {Callsign}, cannot update estimates",
                flight.Callsign);
            return;
        }

        logger.Debug(
            "{Callsign} (no FF) ETA now {LandingEstimate}",
            flight.Callsign,
            landingEstimate);

        flight.UpdateLandingEstimate(landingEstimate.Value);
    }

    void UpdateFlightData(FlightUpdatedNotification notification, Flight flight)
    {
        flight.AircraftType = notification.AircraftType;
        flight.AircraftCategory = notification.AircraftCategory;
        flight.WakeCategory = notification.WakeCategory;

        flight.OriginIdentifier = notification.Origin;
        flight.EstimatedDepartureTime = notification.EstimatedDepartureTime;
        flight.UpdatePosition(notification.Position);
    }
}
