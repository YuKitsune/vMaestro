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
            var sequence = instance.Session.Sequence;

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
                ExactInsertionOptions exactInsertionOptions => InsertExact(request.AirportIdentifier,
                    callsign,
                    performanceData,
                    exactInsertionOptions,
                    instance.Session),

                RelativeInsertionOptions relativeInsertionOptions => InsertRelative(
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    relativeInsertionOptions,
                    instance.Session),

                DepartureInsertionOptions departureInsertionOptions => InsertDeparture(
                    airportConfiguration,
                    request.AirportIdentifier,
                    callsign,
                    performanceData,
                    departureInsertionOptions,
                    instance.Session),

                _ => throw new NotSupportedException($"Unexpected insertion option: \"{request.Options.GetType()}\"")
            };

            // New flights can be inserted in front of existing Unstable and Stable flights on the same runway
            // TODO: Check dependant runways. Backfill this behaviour to the FlightUpdated handler
            // TODO: This insertion behaviour is the same as what's used in FlightUpdated. Consolidate this.
            var targetLandingTime = flight.TargetLandingTime ?? flight.LandingEstimate;

            // Determine the insertion point by landing estimate
            // We assume the above InsertExact, InsertRelative, and InsertDeparture method calls have performed the
            // validation required to prevent conflicts with Frozen flights.
            // TODO: Refactor this to use the feeder fix time if available
            var insertionIndex = sequence.FindIndex(
                f => f.LandingEstimate.IsSameOrAfter(targetLandingTime));

            if (insertionIndex == -1)
                insertionIndex = sequence.Flights.Count;

            sequence.Insert(insertionIndex, flight);

            // Freeze dummy flights as soon as they've been scheduled
            if (flight.IsManuallyInserted)
            {
                flight.SetState(DefaultDummyState, clock);
            }

            logger.Information("Inserted flight {Callsign} with target time {TargetTime:HHmm}", callsign, targetLandingTime);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }

    Flight InsertExact(
        string airportIdentifier,
        string callsign,
        AircraftPerformanceData performanceData,
        ExactInsertionOptions exactInsertionOptions,
        Session session)
    {
        foreach (var runwayIdentifier in exactInsertionOptions.RunwayIdentifiers)
        {
            session.Sequence.ThrowIsTimeIsUnavailable(
                callsign,
                exactInsertionOptions.TargetLandingTime,
                runwayIdentifier);
        }

        var runway = FindRunway(
            session.Sequence,
            exactInsertionOptions.TargetLandingTime,
            exactInsertionOptions.RunwayIdentifiers);

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
                exactInsertionOptions.TargetLandingTime,
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

            flight.SetTargetLandingTime(exactInsertionOptions.TargetLandingTime);

            flight.SetState(DefaultPendingState, clock);
        }

        // Only calculate the landing estimate if the position of the flight is not known (i.e. not coupled to a radar track)
        // If the position is known, source the estimate from the system estimate
        if (flight.Position is null || flight.Position.IsOnGround || flight.IsManuallyInserted)
        {
            flight.UpdateLandingEstimate(exactInsertionOptions.TargetLandingTime);
        }

        return flight;
    }

    Flight InsertRelative(
        string airportIdentifier,
        string callsign,
        AircraftPerformanceData performanceData,
        RelativeInsertionOptions relativeInsertionOptions,
        Session session)
    {
        var referenceFlight = session.Sequence.FindFlight(relativeInsertionOptions.ReferenceCallsign);
        if (referenceFlight is null)
            throw new MaestroException($"{relativeInsertionOptions.ReferenceCallsign} not found");

        if (referenceFlight.State is State.Frozen or State.Landed &&
            relativeInsertionOptions.Position == RelativePosition.Before)
            throw new MaestroException("Cannot insert a flight before a Frozen flight");

        var runway = FindRunway(
            session.Sequence,
            referenceFlight.LandingTime,
            [referenceFlight.AssignedRunwayIdentifier]);

        // TODO: Check if the next runway mode has different separation requirements, and use those if the target time sits within the new mode

        var targetLandingTime = relativeInsertionOptions.Position switch
        {
            RelativePosition.Before => referenceFlight.LandingTime,
            RelativePosition.After => referenceFlight.LandingTime.Add(runway.AcceptanceRate),
            _ => throw new ArgumentOutOfRangeException()
        };

        session.Sequence.ThrowIsTimeIsUnavailable(
            callsign,
            targetLandingTime,
            runway.Identifier);

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

        // Only calculate the landing estimate if the position of the flight is not known (i.e. not coupled to a radar track)
        // If the position is known, source the estimate from the system estimate
        if (flight.Position is null || flight.Position.IsOnGround || flight.IsManuallyInserted)
        {
            flight.UpdateLandingEstimate(targetLandingTime);
        }

        return flight;
    }

    Flight InsertDeparture(
        AirportConfiguration airportConfiguration,
        string airportIdentifier,
        string callsign,
        AircraftPerformanceData performanceData,
        DepartureInsertionOptions departureInsertionOptions,
        Session session)
    {
        // TODO Test Case: When inserting a departure, and the departure exists, they are inserted

        // Calculate the landing estimate based on the provided TakeOffTime + configured ETI
        var enrouteTime = CalculateEnrouteTime(
            airportConfiguration,
            departureInsertionOptions.OriginIdentifier,
            performanceData);
        var landingEstimate = departureInsertionOptions.TakeoffTime.Add(enrouteTime);

        // TODO: Consider feeder fix preferences when assigning a runway
        // TODO: Consider deferring runway selection until the Scheduling phase
        var runway = FindRunway(
            session.Sequence,
            landingEstimate,
            []);

        var flight = session.PendingFlights.SingleOrDefault(f =>
            f.Callsign == callsign &&
            f.AircraftType == performanceData.TypeCode &&
            f.OriginIdentifier == departureInsertionOptions.OriginIdentifier &&
            f.IsFromDepartureAirport);
        if (flight is null)
        {
            // TODO Test Case: When inserting a departure, and the flight does not exist, a dummy flight is created
            // Create a dummy flight
            flight = new Flight(
                callsign,
                performanceData.TypeCode,
                performanceData.AircraftCategory,
                performanceData.WakeCategory,
                airportIdentifier,
                runway.Identifier,
                landingEstimate,
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

            flight.SetState(DefaultPendingState, clock);
        }

        // TODO Test Case: When inserting a departure, TargetLandingTime is not set

        // TODO Test Case: When departure is coupled, system estimate is used
        // TODO Test Case: When departure is uncoupled, landing estimate is calculated by TakeOffTime + ETI
        // Only calculate the landing estimate if the position of the flight is not known (i.e. not coupled to a radar track)
        // If the position is known, source the estimate from the system estimate
        if (flight.Position is null || flight.Position.IsOnGround || flight.IsManuallyInserted)
        {
            flight.UpdateLandingEstimate(landingEstimate);
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
}
