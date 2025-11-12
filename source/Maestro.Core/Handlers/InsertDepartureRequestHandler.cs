using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class InsertDepartureRequestHandler(
    ISessionManager sessionManager,
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

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSession.Session.Sequence;

        var flight = lockedSession.Session.PendingFlights.SingleOrDefault(f => f.Callsign == request.Callsign);
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
        switch (request.Options)
        {
            case ExactInsertionOptions landingTimeOption:
                var runwayMode = sequence.GetRunwayModeAt(landingTimeOption.TargetLandingTime);
                var runwayIdentifier = runwayMode.Runways.FirstOrDefault(r => landingTimeOption.RunwayIdentifiers.Contains(r.Identifier))?.Identifier
                    ?? runwayMode.Default.Identifier;
                flight.SetRunway(runwayIdentifier, manual: true);

                index = sequence.IndexOf(landingTimeOption.TargetLandingTime);
                break;

            case RelativeInsertionOptions relativeInsertionOptions:

                var referenceFlightItem = sequence.FindFlight(relativeInsertionOptions.ReferenceCallsign);
                if (referenceFlightItem is null)
                    throw new MaestroException($"{relativeInsertionOptions.ReferenceCallsign} not found");

                var referenceIndex = sequence.IndexOf(referenceFlightItem);
                if (referenceIndex == -1)
                    throw new MaestroException($"{relativeInsertionOptions.ReferenceCallsign} not found");

                var insertionIndex = relativeInsertionOptions.Position switch
                {
                    RelativePosition.Before => referenceIndex,
                    RelativePosition.After => referenceIndex + 1,
                    _ => throw new ArgumentOutOfRangeException()
                };

                index = insertionIndex;
                break;

            case DepartureInsertionOptions departureInsertionOptions:
                if (flight.EstimatedTimeEnroute is null)
                    throw new MaestroException($"{request.Callsign} has no EET");

                var landingEstimate = departureInsertionOptions.TakeoffTime.Add(flight.EstimatedTimeEnroute.Value);
                flight.UpdateLandingEstimate(landingEstimate);

                // New flights can displace Unstable and Stable flights
                var earliestIndex = sequence.LastIndexOf(f => f.State is not State.Unstable and not State.Stable) + 1;
                var estimateIndex = sequence.FirstIndexOf(f => f.LandingEstimate.IsBefore(landingEstimate)) + 1;

                index = Math.Max(estimateIndex, earliestIndex);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        sequence.Insert(index, flight);

        flight.SetState(State.Stable, clock);

        // Calculate feeder fix estimate based on landing time
        // Need to do this after so that the runway gets assigned
        var feederFixEstimate = GetFeederFixTime(flight);
        if (feederFixEstimate is not null)
            flight.UpdateFeederFixEstimate(feederFixEstimate.Value);

        logger.Information("Inserted departure {Callsign} for {AirportIdentifier}", flight.Callsign, request.AirportIdentifier);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
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
