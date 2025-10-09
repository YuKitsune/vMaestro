using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

// TODO Test Cases:
// - When a flight is inserted, the state is set
// - When a flight is inserted, and it does not exist in the pending list, an exception is thrown
// - When a flight is inserted, with exact insertion options, the position in the sequence is set
// - When a flight is inserted, with exact insertion options, the landing time and runway are set
// - When a flight is inserted, before another flight, the position in the sequence is set
// - When a flight is inserted, before another flight, the flight is inserted before the reference flight, and the reference flight and any trailing conflicts are delayed
// - When a flight is inserted, after another flight, the position in the sequence is set
// - When a flight is inserted, after another flight, the flight is inserted behind the reference flight, and any trailing conflicts are delayed
// - When a flight is inserted, between two frozen flights, without enough space between them (2x landing rate), an exception is thrown

public class InsertDepartureRequestHandler(
    ISessionManager sessionManager,
    IArrivalLookup arrivalLookup,
    IClock clock,
    IMediator mediator)
    : IRequestHandler<InsertDepartureRequest>
{
    public async Task Handle(InsertDepartureRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

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

        sequence.Depart(flight, request.Options);
        flight.SetState(State.Stable, clock);

        // Calculate feeder fix estimate based on landing time
        // Need to do this after so that the runway gets assigned
        var feederFixEstimate = GetFeederFixTime(flight);
        if (feederFixEstimate is not null)
            flight.UpdateFeederFixEstimate(feederFixEstimate.Value);

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
