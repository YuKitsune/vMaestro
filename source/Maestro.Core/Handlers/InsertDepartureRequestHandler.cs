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

public class InsertDepartureRequestHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IArrivalLookup arrivalLookup,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<InsertDepartureRequest>
{
    public async Task Handle(InsertDepartureRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying InsertDepartureRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var flight = instance.Session.PendingFlights.SingleOrDefault(f => f.Callsign == request.Callsign);
            if (flight is null)
            {
                // TODO: Confirm what should happen in this case
                // The UI seems to accept manual input
                // Maybe use Aircraft type to determine a speed and figure out a landing time from there?
                throw new MaestroException($"{request.Callsign} was not found in the pending list.");
            }

            if (!flight.IsFromDepartureAirport)
                throw new MaestroException($"{request.Callsign} is not from a departure airport.");

            int index;
            Runway runway;

            switch (request.Options)
            {
                case ExactInsertionOptions landingTimeOption:
                {
                    var runwayMode = sequence.GetRunwayModeAt(landingTimeOption.TargetLandingTime);
                    runway = runwayMode.Runways.FirstOrDefault(r =>
                                 landingTimeOption.RunwayIdentifiers.Contains(r.Identifier))
                             ?? runwayMode.Default;

                    index = sequence.IndexOf(landingTimeOption.TargetLandingTime);
                    break;
                }

                case RelativeInsertionOptions relativeInsertionOptions:
                {
                    var referenceFlight = sequence.FindFlight(relativeInsertionOptions.ReferenceCallsign);
                    if (referenceFlight is null)
                        throw new MaestroException($"{relativeInsertionOptions.ReferenceCallsign} not found");

                    var referenceIndex = sequence.IndexOf(referenceFlight);
                    if (referenceIndex == -1)
                        throw new MaestroException($"{relativeInsertionOptions.ReferenceCallsign} not found");

                    var insertionIndex = relativeInsertionOptions.Position switch
                    {
                        RelativePosition.Before => referenceIndex,
                        RelativePosition.After => referenceIndex + 1,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    var runwayMode = sequence.GetRunwayModeAt(referenceFlight.LandingTime);
                    runway =
                        runwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlight.AssignedRunwayIdentifier)
                        ?? runwayMode.Default;

                    index = insertionIndex;
                    break;
                }

                case DepartureInsertionOptions departureInsertionOptions:
                {
                    if (flight.EstimatedTimeEnroute is null)
                        throw new MaestroException($"{request.Callsign} has no EET");

                    var landingEstimate = departureInsertionOptions.TakeoffTime.Add(flight.EstimatedTimeEnroute.Value);
                    flight.UpdateLandingEstimate(landingEstimate);

                    // TODO: We do this a lot, extract this into a separate service
                    var airportConfiguration = airportConfigurationProvider
                        .GetAirportConfigurations()
                        .SingleOrDefault(a => a.Identifier == request.AirportIdentifier);
                    if (airportConfiguration is null)
                        throw new MaestroException($"Couldn't find airport configuration for {request.AirportIdentifier}");

                    var runwayMode = sequence.GetRunwayModeAt(landingEstimate);
                    runway = FindBestRunway(airportConfiguration, runwayMode, flight.FeederFixIdentifier);

                    // New flights can be inserted in front of existing Unstable and Stable flights on the same runway
                    var earliestInsertionIndex = sequence.FindLastIndex(f =>
                        f.State is not State.Unstable and not State.Stable &&
                        f.AssignedRunwayIdentifier == runway.Identifier) + 1;

                    index = sequence.FindIndex(
                        earliestInsertionIndex,
                        f => f.LandingEstimate.IsAfter(landingEstimate));

                    // If no flight has a later estimate, insert at the end
                    if (index == -1)
                        index = Math.Max(earliestInsertionIndex, sequence.Flights.Count);

                    index = Math.Max(earliestInsertionIndex, index);

                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }

            flight.SetRunway(runway.Identifier, manual: true);
            sequence.Insert(index, flight);

            flight.SetState(State.Stable, clock);

            // Calculate feeder fix estimate based on landing time
            // Need to do this after so that the runway gets assigned
            var feederFixEstimate = GetFeederFixTime(flight);
            if (feederFixEstimate is not null)
                flight.UpdateFeederFixEstimate(feederFixEstimate.Value);

            logger.Information("Inserted departure {Callsign} for {AirportIdentifier}", flight.Callsign, request.AirportIdentifier);

            instance.Session.PendingFlights.Remove(flight);
            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }

    Runway FindBestRunway(AirportConfiguration airportConfiguration, RunwayMode runwayMode, string? feederFixIdentifier)
    {
        if (!string.IsNullOrEmpty(feederFixIdentifier) &&
            airportConfiguration.PreferredRunways.TryGetValue(feederFixIdentifier, out var preferredRunways))
        {
            return runwayMode.Runways
                       .FirstOrDefault(r => preferredRunways.Contains(r.Identifier))
                   ?? runwayMode.Default;
        }

        return runwayMode.Default;
    }

    DateTimeOffset? GetFeederFixTime(Flight flight)
    {
        var arrivalInterval = arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.AssignedArrivalIdentifier,
            flight.AssignedRunwayIdentifier,
            flight.AircraftType,
            flight.AircraftCategory);
        if (arrivalInterval is null)
            return null;

        return flight.LandingEstimate.Subtract(arrivalInterval.Value);
    }
}
