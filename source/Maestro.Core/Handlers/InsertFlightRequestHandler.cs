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

// TODO: Consider splitting this up into multiple handlers somehow.
//  Real system seems to have functions for Insert Dummy, Insert Pending, Insert Overshoot, and Insert Departure separately
//  There's going to be repetition, but it's probably for the best.

// TODO: Once consolidated, we need to insert by ETA_FF rather than ETA

public class InsertFlightRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IPerformanceLookup performanceLookup,
    IAirportConfigurationProvider airportConfigurationProvider,
    IArrivalLookup arrivalLookup,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<InsertFlightRequest>
{
    const int MaxCallsignLength = 12; // TODO: Verify the VATSIM limit

    // TODO: Make these configurable
    const string DefaultAircraftType = "B738";
    const State DefaultPendingState = State.Stable;
    const State DefaultDummyState = State.Frozen;

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
                ? DefaultAircraftType
                : request.AircraftType;

            var performanceData = performanceLookup.GetPerformanceDataFor(aircraftType);

            var flight = request.Options switch
            {
                ExactInsertionOptions exactInsertionOptions => InsertExact(
                    instance.Session,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    exactInsertionOptions.TargetLandingTime,
                    exactInsertionOptions.RunwayIdentifiers),

                RelativeInsertionOptions relativeInsertionOptions => InsertRelative(
                    instance.Session,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    relativeInsertionOptions.ReferenceCallsign,
                    relativeInsertionOptions.Position),

                DepartureInsertionOptions departureInsertionOptions => InsertDeparture(
                    instance.Session,
                    airportConfiguration,
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

        // Check if flight already exists in sequence
        CheckAndRemoveExistingFlight(session.Sequence, callsign);

        var flight = session.PendingFlights.SingleOrDefault(f =>
            f.Callsign == callsign &&
            f.AircraftType == performanceData.TypeCode);
        if (flight is null)
        {
            // TODO Test Case: When inserting exact, and the flight does not exist, a dummy flight is created
            // Create a dummy flight
            flight = new Flight(
                callsign,
                performanceData.TypeCode,
                performanceData.AircraftCategory,
                performanceData.WakeCategory,
                airportIdentifier,
                runway.Identifier,
                targetLandingTime,
                State.Stable);

            var approachType = FindApproachType(
                airportIdentifier,
                flight.FeederFixIdentifier,
                flight.Fixes,
                runway.Identifier,
                performanceData);
            flight.SetApproachType(approachType);
        }
        else
        {
            session.PendingFlights.Remove(flight);

            flight.SetRunway(runway.Identifier, manual: true);

            var approachType = FindApproachType(
                airportIdentifier,
                flight.FeederFixIdentifier,
                flight.Fixes,
                runway.Identifier,
                performanceData);
            flight.SetApproachType(approachType);

            flight.SetTargetLandingTime(targetLandingTime);

            flight.SetState(DefaultPendingState, clock);
        }

        // Only update the landing estimate if the position of the flight is not known (i.e. not coupled to a radar track)
        // If the position is known, source the estimate from the system estimate
        if (flight.Position is null || flight.Position.IsOnGround || flight.IsManuallyInserted)
        {
            flight.UpdateLandingEstimate(targetLandingTime);

            // Update ETA_FF if the flight has a feeder fix set
            if (!string.IsNullOrEmpty(flight.FeederFixIdentifier))
            {
                var arrivalInterval = arrivalLookup.GetArrivalInterval(flight);
                if (arrivalInterval is not null)
                {
                    var feederFixEstimate = targetLandingTime.Subtract(arrivalInterval.Value);
                    flight.UpdateFeederFixEstimate(feederFixEstimate);
                }
            }
        }

        // Calculate the insertion index based on the landing time.
        // Note that we want to use the scheduled landing time here (STA) as the controller is requesting that the flight
        // be specifically inserted at the specified time.
        // Manually inserted flights can displace Unstable, Stable, and SuperStable flights.
        // The above call to ThrowIsTimeIsUnavailable will ensure the landing time doesn't conflict with any frozen flights.
        // TODO: Refactor this to use the feeder fix time if available
        var insertionIndex = session.Sequence.FindIndex(
            f => f.LandingTime.IsSameOrAfter(targetLandingTime));

        if (insertionIndex == -1)
            insertionIndex = session.Sequence.Flights.Count;

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(DefaultDummyState, clock);
        }

        return flight;
    }

    Flight InsertRelative(
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

        // TODO: Check if the next runway mode has different separation requirements, and use those if the target time sits within the new mode

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

        var flight = session.PendingFlights.SingleOrDefault(f =>
            f.Callsign == callsign &&
            f.AircraftType == performanceData.TypeCode);
        if (flight is null)
        {
            // Create a dummy flight
            flight = new Flight(
                callsign,
                performanceData.TypeCode,
                performanceData.AircraftCategory,
                performanceData.WakeCategory,
                airportIdentifier,
                runway.Identifier,
                targetLandingTime,
                State.Stable);

            var approachType = FindApproachType(
                airportIdentifier,
                flight.FeederFixIdentifier,
                flight.Fixes,
                runway.Identifier,
                performanceData);
            flight.SetApproachType(approachType);
        }
        else
        {
            session.PendingFlights.Remove(flight);

            flight.SetRunway(runway.Identifier, manual: true);

            var approachType = FindApproachType(
                airportIdentifier,
                flight.FeederFixIdentifier,
                flight.Fixes,
                runway.Identifier,
                performanceData);
            flight.SetApproachType(approachType);

            flight.SetTargetLandingTime(targetLandingTime);

            flight.SetState(DefaultPendingState, clock);
        }

        // Only set the landing estimate if the position of the flight is not known (i.e. not coupled to a radar track)
        // If the position is known, source the estimate from the system estimate
        if (flight.Position is null || flight.Position.IsOnGround || flight.IsManuallyInserted)
        {
            flight.UpdateLandingEstimate(targetLandingTime);

            // Update ETA_FF if the flight has a feeder fix set
            if (!string.IsNullOrEmpty(flight.FeederFixIdentifier))
            {
                var arrivalInterval = arrivalLookup.GetArrivalInterval(flight);
                if (arrivalInterval is not null)
                {
                    var feederFixEstimate = targetLandingTime.Subtract(arrivalInterval.Value);
                    flight.UpdateFeederFixEstimate(feederFixEstimate);
                }
            }
        }

        // Calculate the insertion index based on the landing time.
        // Note that we want to use the scheduled landing time here (STA) as the controller is requesting that the flight
        // be specifically inserted at the specified time.
        // Manually inserted flights can displace Unstable, Stable, and SuperStable flights.
        // The above call to ThrowIsTimeIsUnavailable will ensure the landing time doesn't conflict with any frozen flights.
        // TODO: Refactor this to use the feeder fix time if available
        var insertionIndex = session.Sequence.FindIndex(
            f => f.LandingTime.IsSameOrAfter(targetLandingTime));

        if (insertionIndex == -1)
            insertionIndex = session.Sequence.Flights.Count;

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(DefaultDummyState, clock);
        }

        return flight;
    }

    Flight InsertDeparture(
        Session session,
        AirportConfiguration airportConfiguration,
        string airportIdentifier,
        string callsign,
        AircraftPerformanceData performanceData,
        string originIdentifier,
        DateTimeOffset takeoffTime)
    {
        // TODO: Don't allow departures to overtake SuperStable flights

        // Calculate the landing estimate based on the provided TakeOffTime + configured ETI
        var enrouteTime = CalculateEnrouteTime(
            airportConfiguration,
            originIdentifier,
            performanceData);
        var landingEstimate = takeoffTime.Add(enrouteTime);

        // TODO: Consider feeder fix preferences when assigning a runway
        // TODO: Consider deferring runway selection until the Scheduling phase
        var runway = FindRunway(
            session.Sequence,
            landingEstimate,
            []);

        // Check if flight already exists in sequence
        CheckAndRemoveExistingFlight(session.Sequence, callsign);

        var flight = session.PendingFlights.SingleOrDefault(f =>
            f.Callsign == callsign &&
            f.AircraftType == performanceData.TypeCode &&
            f.OriginIdentifier == originIdentifier &&
            f.IsFromDepartureAirport);
        if (flight is null)
        {
            // Create a dummy flight
            flight = new Flight(
                callsign,
                performanceData.TypeCode,
                performanceData.AircraftCategory,
                performanceData.WakeCategory,
                airportIdentifier,
                runway.Identifier,
                landingEstimate,
                State.Stable); // Set to Stable initially to allow scheduling to occur

            var approachType = FindApproachType(
                airportIdentifier,
                flight.FeederFixIdentifier,
                flight.Fixes,
                runway.Identifier,
                performanceData);
            flight.SetApproachType(approachType);
        }
        else
        {
            session.PendingFlights.Remove(flight);

            flight.SetRunway(runway.Identifier, manual: true);

            var approachType = FindApproachType(
                airportIdentifier,
                flight.FeederFixIdentifier,
                flight.Fixes,
                runway.Identifier,
                performanceData);
            flight.SetApproachType(approachType);

            // Departures remain unstable as their landing estimate will become more accurate as they depart, couple, and climb
            flight.SetState(State.Unstable, clock);
        }

        // Only use the calculated the landing estimate if the position of the flight is not known (i.e. not coupled to a radar track)
        // If the position is known, source the estimate from the system estimate
        if (flight.Position is null || flight.Position.IsOnGround || flight.IsManuallyInserted)
        {
            flight.UpdateLandingEstimate(landingEstimate);

            // Update ETA_FF if the flight has a feeder fix set
            if (!string.IsNullOrEmpty(flight.FeederFixIdentifier))
            {
                var arrivalInterval = arrivalLookup.GetArrivalInterval(flight);
                if (arrivalInterval is not null)
                {
                    var feederFixEstimate = landingEstimate.Subtract(arrivalInterval.Value);
                    flight.UpdateFeederFixEstimate(feederFixEstimate);
                }
            }
        }

        // Departures can't overtake SuperStable flights, but they can overtake Unstable and Stable flights
        var earliestInsertionIndex = session.Sequence.FindLastIndex(f =>
            f.State is not State.Unstable and not State.Stable &&
            f.AssignedRunwayIdentifier == runway.Identifier) + 1;

        // Determine the insertion point by landing estimate
        // Note that we want to use the estimate in this case to ensure the flight is fairly sequenced
        // on a first-come first-served basis.
        // TODO: Refactor this to use the feeder fix time if available
        var insertionIndex = session.Sequence.FindIndex(
            earliestInsertionIndex,
            f => f.LandingEstimate.IsAfter(landingEstimate));
        if (insertionIndex == -1)
            insertionIndex = Math.Min(earliestInsertionIndex, session.Sequence.Flights.Count);

        session.Sequence.Insert(insertionIndex, flight);

        // Freeze dummy flights as soon as they've been scheduled
        if (flight.IsManuallyInserted)
        {
            flight.SetState(DefaultDummyState, clock);
        }

        return flight;
    }

    TimeSpan CalculateEnrouteTime(AirportConfiguration airportConfiguration, string departureIdentifier, AircraftPerformanceData performanceData)
    {
        var departureConfiguration = airportConfiguration.DepartureAirports.SingleOrDefault(d => d.Identifier == departureIdentifier);
        if (departureConfiguration is null)
            throw new MaestroException($"{departureIdentifier} is not a configured departure airport");

        var matchingTime = departureConfiguration.FlightTimes.FirstOrDefault(t =>
            (t.AircraftType is SpecificAircraftTypeConfiguration c1 && c1.TypeCode == performanceData.TypeCode) ||
            (t.AircraftType is AircraftCategoryConfiguration c2 && c2.Category == performanceData.AircraftCategory) ||
            t.AircraftType is AllAircraftTypesConfiguration);

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
        string feederFixIdentifier,
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
        if (existingFlight is not null)
        {
            if (existingFlight.State == State.Landed)
            {
                sequence.Remove(existingFlight);
            }
            else
            {
                throw new MaestroException($"Cannot insert {callsign} as it already exists in the sequence");
            }
        }
    }
}
