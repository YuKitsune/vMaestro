using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
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
    IAirportConfigurationProvider airportConfigurationProvider,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<InsertFlightRequest>
{
    const int MaxCallsignLength = 12; // TODO: Verify the VATSIM limit

    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        // TODO: Make configurable
        const string DefaultAircraftType = "B738";

        // TODO Test Case: When connected to server, request is relayed to master
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

            // TODO Test Case: Callsign is normalized and truncated
            // TODO Test Case: When no callsign is provided, dummy callsign is used
            var callsign = request.Callsign?.ToUpperInvariant().Truncate(MaxCallsignLength)!;
            var isDummyFlight = string.IsNullOrWhiteSpace(callsign);
            if (isDummyFlight)
                callsign = instance.Session.NewDummyCallsign();

            // TODO Test Case: When no aircraft type is provided, default is used
            var aircraftType = string.IsNullOrEmpty(request.AircraftType)
                ? DefaultAircraftType
                : request.AircraftType;

            string[] assignableRunways;
            DateTimeOffset landingEstimate;
            DateTimeOffset? targetLandingTime = null;

            if (request.Options is ExactInsertionOptions exactInsertionOptions)
            {
                // TODO Test Case: Exact time, target time is set
                // TODO Test Case: Exact time, relevant runway is assigned
                // TODO Test Case: Exact time, and pending flight is found, pending flight is inserted
                // TODO Test Case: Exact time, no flight is found, dummy is inserted
                // TODO Test Case: Exact time, and flight is coupled, system estimates are used
                // TODO Test Case: Exact time, flight is positioned by target time
                // TODO Test Case: Exact time, flight is sequenced by target time
                // TODO Test Case: Exact time, occupied by Frozen flight, exception is thrown
                // TODO Test Case: Exact time, occupied by Slot, exception is thrown
                // TODO Test Case: Exact time, occupied by Runway change, exception is thrown
            }

            else if (request.Options is RelativeInsertionOptions relativeInsertionOptions)
            {
                var referenceFlight = sequence.FindFlight(relativeInsertionOptions.ReferenceCallsign);
                if (referenceFlight is null)
                    throw new MaestroException($"{relativeInsertionOptions.ReferenceCallsign} not found");

                // TODO Test Case: Cannot insert before frozen flights
                if (referenceFlight.State is State.Frozen or State.Landed &&
                    relativeInsertionOptions.Position == RelativePosition.Before)
                {
                    throw new MaestroException("Cannot insert a flight before a Frozen flight");
                }

                // TODO Test Case: Can insert after frozen flights

                var runwayMode = sequence.GetRunwayModeAt(referenceFlight.LandingTime);
                var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlight.AssignedRunwayIdentifier) ?? runwayMode.Default;

                // TODO: Check if the next runway mode has different separation requirements, and use those if the target time sits within the new mode

                // TODO Test Case: When inserting before another flight, the target time is the reference flights landing time
                // TODO Test Case: When inserting before another flight, the inserted flight is sequenced ahead of the reference flight
                // TODO Test Case: When inserting after another flight, the target time is the reference flights landing time + landing rate
                // TODO Test Case: When inserting after another flight, the inserted flight is sequenced behind the reference flight
                targetLandingTime = relativeInsertionOptions.Position switch
                {
                    RelativePosition.Before => referenceFlight.LandingTime,
                    RelativePosition.After => referenceFlight.LandingTime.Add(runway.AcceptanceRate),
                    _ => throw new ArgumentOutOfRangeException()
                };

                landingEstimate = targetLandingTime.Value;
            }

            else if (request.Options is DepartureInsertionOptions departureOptions)
            {
                // TODO Test Case: When inserting a departure, and the departure exists, they are inserted
                var flight = instance.Session.PendingFlights.SingleOrDefault(f =>
                    f.Callsign == callsign &&
                    f.AircraftType == aircraftType &&
                    f.OriginIdentifier == departureOptions.OriginIdentifier &&
                    f.IsFromDepartureAirport);
                if (flight is null || isDummyFlight)
                {
                    // TODO Test Case: When inserting a departure, and the flight does not exist, a dummy flight is created
                    // TODO: Create a dummy flight
                }

                // TODO Test Case: When departure is uncoupled, landing estimate is calculated by TakeOffTime + ETI
                // Only calculate the landing estimate if the position of the flight is not known (i.e. not coupled to a radar track)
                // If the position is known, source the estimate from the system estimate
                if (flight.Position is null || flight.Position.IsOnGround || isDummyFlight)
                {
                    var enrouteTime = CalculateEnrouteTime(airportConfiguration, flight);
                    var landingEstimate = departureOptions.TakeoffTime.Add(enrouteTime);
                    flight.UpdateLandingEstimate(landingEstimate);
                }

                // TODO Test Case: When departure is coupled, system estimate is used

                // TODO Test Case: When inserting a departure, TargetLandingTime is not set
            }

            // TODO: Don't allocate a runway yet, let the Sequence deal with it
            var runwayMode = sequence.GetRunwayModeAt(flight.LandingEstimate);
            runway = FindBestRunway(airportConfiguration, runwayMode, flight.FeederFixIdentifier);

            // New flights can be inserted in front of existing Unstable and Stable flights on the same runway
            // TODO: Check dependant runways. Backfill this behaviour to the FlightUpdated handler
            // TODO Test Case: When inserting a flight, and it's estimate is ahead of an Unstable or Stable flight, it is inserted in front of that flight
            // TODO Test Case: When inserting a flight, and it's estimate is ahead of a SuperStable, Frozen, or Landed flight, it is inserted behind that flight
            var earliestInsertionIndex = sequence.FindLastIndex(f =>
                f.State is not State.Unstable and not State.Stable &&
                f.AssignedRunwayIdentifier == runway.Identifier) + 1;

            index = sequence.FindIndex(
                earliestInsertionIndex,
                f => f.LandingEstimate.IsAfter(flight.LandingEstimate));

            // TODO: We need to make sure we're doing this anywhere else we're inserting a flight
            // If there are no flights after this one, add them to the back of the queue
            if (index == -1)
                index = sequence.Flights.Count;

            index = Math.Max(earliestInsertionIndex, index);

            // TODO Test Case: When inserting a pending flight, ETA_FF is calculated

            // TODO Test Case: When dummy is inserted, it's frozen
            // TODO Test Case: When pending flight is inserted, it's made stable (as per configuration)
            var state = State.Frozen; // TODO: Make this configurable

            // int index;
            // string runwayIdentifier;
            // DateTimeOffset landingTime;

            // switch (request.Options)
            // {
            //     case ExactInsertionOptions exactInsertionOptions:
            //     {
            //         index = sequence.IndexOf(exactInsertionOptions.TargetLandingTime);
            //
            //         var runwayMode = sequence.GetRunwayModeAt(exactInsertionOptions.TargetLandingTime);
            //         var runway = runwayMode.Runways.FirstOrDefault(r => exactInsertionOptions.RunwayIdentifiers.Contains(r.Identifier)) ?? runwayMode.Default;
            //         runwayIdentifier = runway.Identifier;
            //         landingTime = exactInsertionOptions.TargetLandingTime;
            //         break;
            //     }
            //     case RelativeInsertionOptions relativeInsertionOptions:
            //     {
            //         var referenceFlight = sequence.FindFlight(relativeInsertionOptions.ReferenceCallsign);
            //         if (referenceFlight == null)
            //             throw new MaestroException($"Reference flight {relativeInsertionOptions.ReferenceCallsign} not found in sequence.");
            //
            //         index = relativeInsertionOptions.Position switch
            //         {
            //             RelativePosition.Before => sequence.IndexOf(referenceFlight.LandingTime),
            //             RelativePosition.After => sequence.IndexOf(referenceFlight.LandingTime) + 1,
            //             _ => throw new ArgumentOutOfRangeException()
            //         };
            //
            //         runwayIdentifier = referenceFlight.AssignedRunwayIdentifier;
            //
            //         var runwayMode = sequence.GetRunwayModeAt(referenceFlight.LandingTime);
            //         var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == runwayIdentifier) ?? runwayMode.Default;
            //
            //         landingTime = relativeInsertionOptions.Position switch
            //         {
            //             RelativePosition.Before => referenceFlight.LandingTime,
            //             RelativePosition.After => referenceFlight.LandingTime.Add(runway.AcceptanceRate),
            //             _ => throw new ArgumentOutOfRangeException()
            //         };
            //         break;
            //     }
            //     default:
            //         throw new NotSupportedException($"Cannot insert flight with {request.Options.GetType().Name}");
            // }

            // var flight = new Flight(
            //     callsign,
            //     request.AircraftType,
            //     request.AirportIdentifier,
            //     runwayIdentifier,
            //     landingTime,
            //     state);

            sequence.Insert(index, flight);

            logger.Information("Inserted dummy flight {Callsign} for {AirportIdentifier}", callsign, request.AirportIdentifier);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }

    TimeSpan CalculateEnrouteTime(AirportConfiguration airportConfiguration, Flight flight)
    {
        var departureAirportConfiguration = airportConfiguration.DepartureAirports.SingleOrDefault(d => d.Identifier == flight.OriginIdentifier);
        if (departureAirportConfiguration is null)
            throw new MaestroException($"{flight.Callsign} is not from a departure airport");

        var matchingTime = departureAirportConfiguration.FlightTimes.FirstOrDefault(t =>
            (t.AircraftType is SpecificAircraftTypeConfiguration c1 && c1.TypeCode == flight.AircraftType) ||
            (t.AircraftType is AircraftCategoryConfiguration c2 && c2.Category == flight.AircraftCategory) ||
            t.AircraftType is AllAircraftTypesConfiguration);

        if (matchingTime is not null)
            return matchingTime.AverageFlightTime;

        var averageSeconds = departureAirportConfiguration.FlightTimes.Average(t => t.AverageFlightTime.TotalSeconds);
        return TimeSpan.FromSeconds(averageSeconds);
    }

    DateTimeOffset GetEarliestLandingTimeAfter(Sequence sequence, Flight referenceFlight, string runwayIdentifier)
    {
        var referenceFlightRunwayMode = sequence.GetRunwayModeAt(referenceFlight.LandingTime);

        var referenceFlightRunway = referenceFlightRunwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlight.AssignedRunwayIdentifier) ?? referenceFlightRunwayMode.Default;

        var targetRunway = referenceFlightRunwayMode.Runways.FirstOrDefault(r => r.Identifier == runwayIdentifier) ?? referenceFlightRunwayMode.Default;
        var requiredSeparation = targetRunway.Dependencies
            .FirstOrDefault(d => d.RunwayIdentifier == referenceFlightRunway.Identifier)
            ?.Separation ?? targetRunway.AcceptanceRate;

        var earliestLandingTime = referenceFlight.LandingTime.Add(requiredSeparation);
        if (sequence is { LastLandingTimeForCurrentMode: not null, FirstLandingTimeForNewMode: not null } && earliestLandingTime.IsAfter(sequence.LastLandingTimeForCurrentMode.Value))
        {
            // Delayed into runway change
            // TODO: Need to re-calculate the required separation
            return sequence.FirstLandingTimeForNewMode.Value;
        }

        return earliestLandingTime;
    }
}
