using Maestro.Core.Connectivity;
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

        var flight = sequence.PendingFlights.SingleOrDefault(f =>
            f.Callsign == request.Callsign);

        if (flight is null)
        {
            // TODO: Confirm what should happen in this case
            // The UI seems to accept manual input
            // Maybe use Aircraft type to determine a speed and figure out a landing time from there?
            throw new MaestroException($"{request.Callsign} was not found in the pending list.");
        }

        if (!flight.IsFromDepartureAirport)
            throw new MaestroException($"{request.Callsign} is not from a departure airport.");

        sequence.Depart(flight, request.Options);
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
