using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class InsertFlightRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IPerformanceLookup performanceLookup,
    IAirportConfigurationProvider airportConfigurationProvider,
    ITrajectoryService trajectoryService,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<InsertFlightRequest>
{
    const int MaxCallsignLength = 12;

    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying InsertFlightRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var callsign = request.Callsign?.Trim().ToUpperInvariant().Truncate(MaxCallsignLength)!;
            var isDummyFlight = string.IsNullOrWhiteSpace(callsign);
            if (isDummyFlight)
                callsign = session.NewDummyCallsign();

            logger.Verbose("Inserting {Callsign} for {AirportIdentifier}", callsign, request.AirportIdentifier);

            var aircraftType = string.IsNullOrEmpty(request.AircraftType)
                ? airportConfiguration.DefaultAircraftType
                : request.AircraftType!;

            var performanceData = performanceLookup.GetPerformanceDataFor(aircraftType);

            var flight = request.Options switch
            {
                ExactInsertionOptions exactInsertionOptions => InsertExact(
                    airportConfiguration,
                    session,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    exactInsertionOptions.TargetLandingTime,
                    exactInsertionOptions.RunwayIdentifiers),

                RelativeInsertionOptions relativeInsertionOptions => InsertRelative(
                    airportConfiguration,
                    session,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    relativeInsertionOptions.ReferenceCallsign,
                    relativeInsertionOptions.Position),

                DepartureInsertionOptions departureInsertionOptions => InsertDeparture(
                    airportConfiguration,
                    session,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    departureInsertionOptions.OriginIdentifier,
                    departureInsertionOptions.TakeoffTime),

                _ => throw new NotSupportedException($"Unexpected insertion option: \"{request.Options.GetType()}\"")
            };

            logger.Information("Inserted flight {Callsign} with landing time {LandingTime:HHmm} (target time {TargetTime:HHmm}", callsign, flight.LandingTime, flight.TargetLandingTime);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }

    Flight InsertExact(
        AirportConfiguration airportConfiguration,
        Session session,
        string airportIdentifier,
        string callsign,
        AircraftPerformanceData performanceData,
        DateTimeOffset targetLandingTime,
        string[] runwayIdentifiers)
    {
        foreach (var runwayIdentifier in runwayIdentifiers)
        {
            session.Sequence.ThrowIsTimeIsUnavailable(
                callsign,
                targetLandingTime,
                runwayIdentifier);
        }

        var runway = FindRunway(
            session.Sequence,
            targetLandingTime,
            runwayIdentifiers);

        CheckAndRemoveExistingFlight(session.Sequence, callsign);

        var existingPendingFlight = session.PendingFlights.SingleOrDefault(f => f.Callsign == callsign);

        Flight flight;
        if (existingPendingFlight is null)
        {
            // Create a dummy flight if a pending flight couldn't be found
            var trajectory = trajectoryService.GetTrajectory(
                performanceData,
                airportIdentifier,
                null,
                runway.Identifier,
                runway.ApproachType,
                [],
                session.Sequence.UpperWind);

            // TODO: test case - When inserting a dummy flight, at an exact time, FeederFixEstimate is TargetTime - Trajectory.TimeToGo
            flight = new Flight(
                callsign: callsign,
                aircraftType: performanceData.TypeCode,
                aircraftCategory: performanceData.AircraftCategory,
                wakeCategory: performanceData.WakeCategory,
                destinationIdentifier: airportIdentifier,
                assignedRunwayIdentifier: runway.Identifier,
                approachType: runway.ApproachType,
                terminalTrajectory: trajectory,
                targetLandingTime: targetLandingTime,
                state: State.Stable);

            logger.Verbose(
                "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
                callsign,
                runway.Identifier,
                runway.ApproachType,
                trajectory.TimeToGo,
                trajectory.Pressure,
                trajectory.MaxPressure);
        }
        else
        {
            session.PendingFlights.Remove(existingPendingFlight);

            flight = CreateFlightFromPending(
                existingPendingFlight,
                session,
                airportConfiguration,
                airportIdentifier,
                performanceData,
                runway,
                landingEstimate: targetLandingTime);

            flight.SetTargetLandingTime(targetLandingTime);
            flight.SetState(airportConfiguration.DefaultPendingFlightState, clock);
        }

        // Calculate the insertion index based on the landing time.
        // TODO: test case - When inserting a flight, at an exact time, the flight is positioned based on the TargetLandingTime (not the feeder fix estimate)
        var insertionIndex = session.Sequence.FindIndex(
            f => f.LandingTime.IsSameOrAfter(targetLandingTime));

        if (insertionIndex == -1)
            insertionIndex = session.Sequence.Flights.Count;

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(airportConfiguration.DefaultDummyFlightState, clock);
        }

        return flight;
    }

    Flight InsertRelative(
        AirportConfiguration airportConfiguration,
        Session session,
        string airportIdentifier,
        string callsign,
        AircraftPerformanceData performanceData,
        string referenceCallsign,
        RelativePosition position)
    {
        var referenceFlight = session.Sequence.FindFlight(referenceCallsign);
        if (referenceFlight is null)
            throw new MaestroException($"{referenceCallsign} not found");

        if (referenceFlight.State is State.Frozen or State.Landed &&
            position == RelativePosition.Before)
            throw new MaestroException("Cannot insert a flight before a Frozen flight");

        var runway = FindRunway(
            session.Sequence,
            referenceFlight.LandingTime,
            [referenceFlight.AssignedRunwayIdentifier]);

        var targetLandingTime = position switch
        {
            RelativePosition.Before => referenceFlight.LandingTime,
            RelativePosition.After => referenceFlight.LandingTime.Add(runway.AcceptanceRate),
            _ => throw new ArgumentOutOfRangeException()
        };

        session.Sequence.ThrowIsTimeIsUnavailable(
            callsign,
            targetLandingTime,
            runway.Identifier);

        // Check if flight already exists in sequence
        CheckAndRemoveExistingFlight(session.Sequence, callsign);

        var existingPendingFlight = session.PendingFlights.SingleOrDefault(f => f.Callsign == callsign);

        Flight flight;
        if (existingPendingFlight is null)
        {
            // Create a dummy flight if a pending flight couldn't be found
            var trajectory = trajectoryService.GetTrajectory(
                performanceData,
                airportIdentifier,
                null,
                runway.Identifier,
                runway.ApproachType,
                [],
                session.Sequence.UpperWind);

            // TODO: test case - When inserting a dummy flight, relative to another, FeederFixEstimate is ReferenceFlight.LandingTime - AcceptanceRate - Trajectory.TimeToGo
            flight = new Flight(
                callsign: callsign,
                aircraftType: performanceData.TypeCode,
                aircraftCategory: performanceData.AircraftCategory,
                wakeCategory: performanceData.WakeCategory,
                destinationIdentifier: airportIdentifier,
                assignedRunwayIdentifier: runway.Identifier,
                approachType: runway.ApproachType,
                terminalTrajectory: trajectory,
                targetLandingTime: targetLandingTime,
                state: State.Stable);

            logger.Verbose(
                "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
                callsign,
                runway.Identifier,
                runway.ApproachType,
                trajectory.TimeToGo,
                trajectory.Pressure,
                trajectory.MaxPressure);
        }
        else
        {
            session.PendingFlights.Remove(existingPendingFlight);

            flight = CreateFlightFromPending(
                existingPendingFlight,
                session,
                airportConfiguration,
                airportIdentifier,
                performanceData,
                runway,
                landingEstimate: targetLandingTime);

            flight.SetTargetLandingTime(targetLandingTime);
            flight.SetState(airportConfiguration.DefaultPendingFlightState, clock);
        }

        // Calculate the insertion index based on the landing time.
        // TODO: test case - When inserting a flight, relative to another, the flight is positioned based on the TargetLandingTime (not the feeder fix estimate)
        var insertionIndex = session.Sequence.FindIndex(
            f => f.LandingTime.IsSameOrAfter(targetLandingTime));

        if (insertionIndex == -1)
            insertionIndex = session.Sequence.Flights.Count;

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(airportConfiguration.DefaultDummyFlightState, clock);
        }

        return flight;
    }

    Flight InsertDeparture(
        AirportConfiguration airportConfiguration,
        Session session,
        string airportIdentifier,
        string callsign,
        AircraftPerformanceData performanceData,
        string originIdentifier,
        DateTimeOffset takeoffTime)
    {
        // Calculate the landing estimate based on the provided TakeOffTime + configured ETI
        var enrouteTime = CalculateEnrouteTime(
            airportConfiguration,
            originIdentifier,
            performanceData);
        var landingEstimate = takeoffTime.Add(enrouteTime);

        var runway = FindRunway(
            session.Sequence,
            landingEstimate,
            []);

        // Check if flight already exists in sequence
        CheckAndRemoveExistingFlight(session.Sequence, callsign);

        var existingPendingFlight = session.PendingFlights.SingleOrDefault(f =>
            f.Callsign == callsign &&
            f.IsFromDepartureAirport);

        Flight flight;
        if (existingPendingFlight is null)
        {
            // Create a dummy flight if a pending flight couldn't be found
            var trajectory = trajectoryService.GetTrajectory(
                performanceData,
                airportIdentifier,
                null,
                runway.Identifier,
                runway.ApproachType,
                [],
                session.Sequence.UpperWind);

            // TODO: test case - When inserting a dummy flight, from a departure airport, the FeederFixEstimate is TakeOffTime + DepartureETI - Trajectory.TimeToGo
            flight = new Flight(
                callsign: callsign,
                aircraftType: performanceData.TypeCode,
                aircraftCategory: performanceData.AircraftCategory,
                wakeCategory: performanceData.WakeCategory,
                destinationIdentifier: airportIdentifier,
                assignedRunwayIdentifier: runway.Identifier,
                approachType: runway.ApproachType,
                terminalTrajectory: trajectory,
                targetLandingTime: landingEstimate,
                state: State.Stable);

            logger.Verbose(
                "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
                callsign,
                runway.Identifier,
                runway.ApproachType,
                trajectory.TimeToGo,
                trajectory.Pressure,
                trajectory.MaxPressure);
        }
        else
        {
            session.PendingFlights.Remove(existingPendingFlight);

            flight = CreateFlightFromPending(
                existingPendingFlight,
                session,
                airportConfiguration,
                airportIdentifier,
                performanceData,
                runway,
                landingEstimate);

            // Departures remain unstable as their landing estimate will become more accurate as they depart, couple, and climb
            flight.SetState(airportConfiguration.DefaultDepartureFlightState, clock);
        }

        // Departures can't overtake SuperStable flights, but they can overtake Unstable and Stable flights
        var earliestInsertionIndex = session.Sequence.FindLastIndex(f =>
            f.State is not State.Unstable and not State.Stable &&
            f.AssignedRunwayIdentifier == runway.Identifier) + 1;

        // Determine the insertion point by landing estimate (ETA)
        var insertionIndex = session.Sequence.FindIndex(
            earliestInsertionIndex,
            f => f.LandingEstimate.IsAfter(flight.LandingEstimate));
        if (insertionIndex == -1)
            insertionIndex = Math.Min(earliestInsertionIndex, session.Sequence.Flights.Count);

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(airportConfiguration.DefaultDummyFlightState, clock);
        }

        return flight;
    }

    /// <summary>
    /// Creates a full <see cref="Flight"/> from a <see cref="PendingFlight"/> record by looking up
    /// the latest <see cref="FlightDataRecord"/>..
    /// </summary>
    Flight CreateFlightFromPending(
        PendingFlight pendingFlight,
        Session session,
        AirportConfiguration airportConfiguration,
        string airportIdentifier,
        AircraftPerformanceData performanceData,
        Runway runway,
        DateTimeOffset landingEstimate)
    {
        session.FlightDataRecords.TryGetValue(pendingFlight.Callsign, out var flightDataRecord);

        var feederFix = flightDataRecord?.Estimates.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));

        var fixNames = flightDataRecord?.Estimates.Select(e => e.FixIdentifier).ToArray() ?? [];
        var terminalTrajectory = trajectoryService.GetTrajectory(
            performanceData,
            airportIdentifier,
            feederFix?.FixIdentifier,
            runway.Identifier,
            runway.ApproachType,
            fixNames,
            session.Sequence.UpperWind);
        var enrouteTrajectory = trajectoryService.GetEnrouteTrajectory(
            airportIdentifier,
            fixNames,
            feederFix?.FixIdentifier ?? string.Empty);

        // Use the live feeder fix estimate only when coupled to a radar track.
        // For uncoupled flights, estimates can be inaccurate, so we'll use the calculated landingEstimate
        // for exact/relative insertions, or takeoff time + ETI for departures, and derive FeederFixEstimate
        // from landingEstimate - TTG.
        // Live updates via FlightUpdatedHandler will refine both estimates once the flight couples.
        var feederFixEstimate = flightDataRecord?.Position is not null ? feederFix?.Estimate : null;

        var flight = new Flight(
            callsign: pendingFlight.Callsign,
            aircraftType: performanceData.TypeCode,
            aircraftCategory: performanceData.AircraftCategory,
            wakeCategory: performanceData.WakeCategory,
            destinationIdentifier: airportIdentifier,
            originIdentifier: flightDataRecord?.Origin,
            isFromDepartureAirport: pendingFlight.IsFromDepartureAirport,
            estimatedDepartureTime: flightDataRecord?.EstimatedDepartureTime,
            assignedRunwayIdentifier: runway.Identifier,
            approachType: runway.ApproachType,
            terminalTrajectory: terminalTrajectory,
            enrouteTrajectory: enrouteTrajectory,
            feederFixIdentifier: feederFix?.FixIdentifier,
            feederFixEstimate: feederFixEstimate,
            landingEstimate: landingEstimate,
            activatedTime: clock.UtcNow(),
            position: flightDataRecord?.Position);

        logger.Verbose(
            "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
            pendingFlight.Callsign,
            runway.Identifier,
            runway.ApproachType,
            trajectory.TimeToGo,
            trajectory.Pressure,
            trajectory.MaxPressure);

        return flight;
    }

    TimeSpan CalculateEnrouteTime(
        AirportConfiguration airportConfiguration,
        string departureIdentifier,
        AircraftPerformanceData performanceData)
    {
        var departureConfigurations = airportConfiguration.DepartureAirports
            .Where(d => d.Identifier == departureIdentifier && d.Aircraft.Matches(performanceData))
            .ToArray();

        if (departureConfigurations.Length == 0)
        {
            var average = airportConfiguration.DepartureAirports.Average(d => d.EstimatedFlightTimeMinutes);
            return TimeSpan.FromMinutes(average);
        }

        // TODO: Log a warning on multiple matches
        return TimeSpan.FromMinutes(departureConfigurations.Average(d => d.EstimatedFlightTimeMinutes));
    }

    Runway FindRunway(
        Sequence sequence,
        DateTimeOffset targetLandingTime,
        string[] requestedRunwayIdentifiers)
    {
        var runwayMode = sequence.GetRunwayModeAt(targetLandingTime);
        var runway = runwayMode.Runways.FirstOrDefault(r => requestedRunwayIdentifiers.Contains(r.Identifier));
        return runway ?? runwayMode.Default;
    }

    void CheckAndRemoveExistingFlight(Sequence sequence, string callsign)
    {
        var existingFlight = sequence.FindFlight(callsign);
        if (existingFlight is null)
            return;

        if (existingFlight.State != State.Landed)
            throw new MaestroException($"Cannot insert {callsign} as it already exists in the sequence");

        sequence.Remove(existingFlight);
    }
}
