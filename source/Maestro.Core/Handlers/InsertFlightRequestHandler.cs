using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class InsertFlightRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IPerformanceLookup performanceLookup,
    IAirportConfigurationProvider airportConfigurationProvider,
    IArrivalLookup arrivalLookup,
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
            logger.Information("Relaying InsertFlightRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .SingleOrDefault(a => a.Identifier == request.AirportIdentifier);
        if (airportConfiguration is null)
            throw new MaestroException($"Couldn't find airport configuration for {request.AirportIdentifier}");

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var callsign = request.Callsign?.Trim().ToUpperInvariant().Truncate(MaxCallsignLength)!;
            var isDummyFlight = string.IsNullOrWhiteSpace(callsign);
            if (isDummyFlight)
                callsign = instance.Session.NewDummyCallsign();

            var aircraftType = string.IsNullOrEmpty(request.AircraftType)
                ? airportConfiguration.DefaultInsertedFlightAircraftType
                : request.AircraftType;

            var performanceData = performanceLookup.GetPerformanceDataFor(aircraftType);

            var flight = request.Options switch
            {
                ExactInsertionOptions exactInsertionOptions => InsertExact(
                    airportConfiguration,
                    instance.Session,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    exactInsertionOptions.TargetLandingTime,
                    exactInsertionOptions.RunwayIdentifiers),

                RelativeInsertionOptions relativeInsertionOptions => InsertRelative(
                    airportConfiguration,
                    instance.Session,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    relativeInsertionOptions.ReferenceCallsign,
                    relativeInsertionOptions.Position),

                DepartureInsertionOptions departureInsertionOptions => InsertDeparture(
                    airportConfiguration,
                    instance.Session,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    departureInsertionOptions.OriginIdentifier,
                    departureInsertionOptions.TakeoffTime),

                _ => throw new NotSupportedException($"Unexpected insertion option: \"{request.Options.GetType()}\"")
            };

            logger.Information("Inserted flight {Callsign} with landing time {LandingTime:HHmm} (target time {TargetTime:HHmm}", callsign, flight.LandingTime, flight.TargetLandingTime);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
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

        var existingPendingFlight = session.PendingFlights.SingleOrDefault(f =>
            f.Callsign == callsign &&
            f.AircraftType == performanceData.TypeCode);

        Flight flight;
        if (existingPendingFlight is null)
        {
            // Create a dummy flight if a pending flight couldn't be found
            var approachType = FindApproachType(
                airportIdentifier,
                null,
                [],
                runway.Identifier,
                performanceData);

            var trajectory = trajectoryService.GetTrajectory(
                performanceData.TypeCode,
                performanceData.AircraftCategory,
                airportIdentifier,
                null,
                runway.Identifier,
                approachType);

            flight = new Flight(
                callsign: callsign,
                aircraftType: performanceData.TypeCode,
                aircraftCategory: performanceData.AircraftCategory,
                wakeCategory: performanceData.WakeCategory,
                destinationIdentifier: airportIdentifier,
                assignedRunwayIdentifier: runway.Identifier,
                approachType: approachType,
                trajectory: trajectory,
                targetLandingTime: targetLandingTime,
                state: State.Stable);
        }
        else
        {
            session.PendingFlights.Remove(existingPendingFlight);

            var approachType = FindApproachType(
                airportIdentifier,
                existingPendingFlight.FeederFixIdentifier,
                existingPendingFlight.Fixes,
                runway.Identifier,
                performanceData);

            var trajectory = trajectoryService.GetTrajectory(existingPendingFlight, runway.Identifier, approachType);

            // Atomic update: runway + trajectory + ETA + STA_FF
            existingPendingFlight.SetRunway(runway.Identifier, trajectory);
            existingPendingFlight.SetApproachType(approachType, trajectory);
            existingPendingFlight.SetTargetLandingTime(targetLandingTime);
            existingPendingFlight.UpdateFeederFixEstimate(targetLandingTime.Subtract(trajectory.TimeToGo));
            existingPendingFlight.SetState(airportConfiguration.ManuallyInsertedFlightState, clock);

            flight = existingPendingFlight;
        }

        // Calculate the insertion index based on the landing time.
        var insertionIndex = session.Sequence.FindIndex(
            f => f.LandingTime.IsSameOrAfter(targetLandingTime));

        if (insertionIndex == -1)
            insertionIndex = session.Sequence.Flights.Count;

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(airportConfiguration.DummyFlightState, clock);
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

        var existingPendingFlight = session.PendingFlights.SingleOrDefault(f =>
            f.Callsign == callsign &&
            f.AircraftType == performanceData.TypeCode);

        Flight flight;
        if (existingPendingFlight is null)
        {
            // Create a dummy flight if a pending flight couldn't be found
            var approachType = FindApproachType(
                airportIdentifier,
                null,
                [],
                runway.Identifier,
                performanceData);

            var trajectory = trajectoryService.GetTrajectory(
                performanceData.TypeCode,
                performanceData.AircraftCategory,
                airportIdentifier,
                null,
                runway.Identifier,
                approachType);

            flight = new Flight(
                callsign: callsign,
                aircraftType: performanceData.TypeCode,
                aircraftCategory: performanceData.AircraftCategory,
                wakeCategory: performanceData.WakeCategory,
                destinationIdentifier: airportIdentifier,
                assignedRunwayIdentifier: runway.Identifier,
                approachType: approachType,
                trajectory: trajectory,
                targetLandingTime: targetLandingTime,
                state: State.Stable);
        }
        else
        {
            session.PendingFlights.Remove(existingPendingFlight);

            var approachType = FindApproachType(
                airportIdentifier,
                existingPendingFlight.FeederFixIdentifier,
                existingPendingFlight.Fixes,
                runway.Identifier,
                performanceData);

            var trajectory = trajectoryService.GetTrajectory(existingPendingFlight, runway.Identifier, approachType);

            // Atomic update: runway + trajectory + ETA + STA_FF
            existingPendingFlight.SetRunway(runway.Identifier, trajectory);
            existingPendingFlight.SetApproachType(approachType, trajectory);
            existingPendingFlight.SetTargetLandingTime(targetLandingTime);
            existingPendingFlight.UpdateFeederFixEstimate(targetLandingTime.Subtract(trajectory.TimeToGo));
            existingPendingFlight.SetState(airportConfiguration.ManuallyInsertedFlightState, clock);

            flight = existingPendingFlight;
        }

        // Calculate the insertion index based on the landing time.
        var insertionIndex = session.Sequence.FindIndex(
            f => f.LandingTime.IsSameOrAfter(targetLandingTime));

        if (insertionIndex == -1)
            insertionIndex = session.Sequence.Flights.Count;

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(airportConfiguration.DummyFlightState, clock);
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
            f.AircraftType == performanceData.TypeCode &&
            f.OriginIdentifier == originIdentifier &&
            f.IsFromDepartureAirport);

        Flight flight;
        if (existingPendingFlight is null)
        {
            // Create a dummy flight if a pending flight couldn't be found
            var approachType = FindApproachType(
                airportIdentifier,
                null,
                [],
                runway.Identifier,
                performanceData);

            var trajectory = trajectoryService.GetTrajectory(
                performanceData.TypeCode,
                performanceData.AircraftCategory,
                airportIdentifier,
                null,
                runway.Identifier,
                approachType);

            flight = new Flight(
                callsign: callsign,
                aircraftType: performanceData.TypeCode,
                aircraftCategory: performanceData.AircraftCategory,
                wakeCategory: performanceData.WakeCategory,
                destinationIdentifier: airportIdentifier,
                assignedRunwayIdentifier: runway.Identifier,
                approachType: approachType,
                trajectory: trajectory,
                targetLandingTime: landingEstimate,
                state: State.Stable);
        }
        else
        {
            session.PendingFlights.Remove(existingPendingFlight);

            var approachType = FindApproachType(
                airportIdentifier,
                existingPendingFlight.FeederFixIdentifier,
                existingPendingFlight.Fixes,
                runway.Identifier,
                performanceData);

            var trajectory = trajectoryService.GetTrajectory(existingPendingFlight, runway.Identifier, approachType);

            // Atomic update: runway + trajectory + ETA + STA_FF
            existingPendingFlight.SetRunway(runway.Identifier, trajectory);
            existingPendingFlight.SetApproachType(approachType, trajectory);
            existingPendingFlight.UpdateFeederFixEstimate(landingEstimate.Subtract(trajectory.TimeToGo));

            // Departures remain unstable as their landing estimate will become more accurate as they depart, couple, and climb
            existingPendingFlight.SetState(airportConfiguration.InitialDepartureFlightState, clock);

            flight = existingPendingFlight;
        }

        // Departures can't overtake SuperStable flights, but they can overtake Unstable and Stable flights
        var earliestInsertionIndex = session.Sequence.FindLastIndex(f =>
            f.State is not State.Unstable and not State.Stable &&
            f.AssignedRunwayIdentifier == runway.Identifier) + 1;

        // Determine the insertion point by landing estimate (ETA)
        var insertionIndex = session.Sequence.FindIndex(
            earliestInsertionIndex,
            f => f.LandingEstimate.IsAfter(landingEstimate));
        if (insertionIndex == -1)
            insertionIndex = Math.Min(earliestInsertionIndex, session.Sequence.Flights.Count);

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(airportConfiguration.DummyFlightState, clock);
        }

        return flight;
    }

    TimeSpan CalculateEnrouteTime(AirportConfiguration airportConfiguration, string departureIdentifier, AircraftPerformanceData performanceData)
    {
        var departureConfiguration = airportConfiguration.DepartureAirports.SingleOrDefault(d => d.Identifier == departureIdentifier);
        if (departureConfiguration is null)
            throw new MaestroException($"{departureIdentifier} is not a configured departure airport");

        var matchingTime = departureConfiguration.FlightTimes.FirstOrDefault(t =>
            (t.Aircraft is SpecificAircraftTypeDescriptor c1 && c1.TypeCode == performanceData.TypeCode) ||
            (t.Aircraft is AircraftCategoryDescriptor c2 && c2.AircraftCategory == performanceData.AircraftCategory) ||
            (t.Aircraft is WakeCategoryDescriptor c3 && c3.WakeCategory == performanceData.WakeCategory) ||
            t.Aircraft is AllAircraftTypesDescriptor);

        if (matchingTime is not null)
            return matchingTime.AverageFlightTime;

        var averageSeconds = departureConfiguration.FlightTimes.Average(t => t.AverageFlightTime.TotalSeconds);
        return TimeSpan.FromSeconds(averageSeconds);
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

    string FindApproachType(
        string airportIdentifier,
        string? feederFixIdentifier,
        FixEstimate[] fixes,
        string runwayIdentifier,
        AircraftPerformanceData performanceData)
    {
        var arrivals = arrivalLookup.GetApproachTypes(
            airportIdentifier,
            feederFixIdentifier,
            fixes.Select(x => x.FixIdentifier).ToArray(),
            runwayIdentifier,
            performanceData.TypeCode,
            performanceData.AircraftCategory);
        return arrivals.FirstOrDefault() ?? string.Empty;
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
