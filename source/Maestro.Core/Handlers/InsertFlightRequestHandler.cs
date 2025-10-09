using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

// TODO:
// - [X] Refactor dummy flight into a separate type
// - [X] Separate pending flight insertion from dummy flight insertion
// - [ ] Update WPF to support new DummyFlight type
// - [ ] Test


// TODO: Test cases
// - When a flight is inserted, the state is set
// - When a flight is inserted, and a callsign is provided, the provided callsign is used
// - When a flight is inserted, and an invalid callsign is provided, it is made uppercase and truncated
// - When a flight is inserted, and an aircraft type is provided, the provided aircraft type is used
// - When a flight is inserted, with exact insertion options, the position in the sequence is updated
// - When a flight is inserted, with exact insertion options, the landing time and runway are updated
// - When a flight is inserted, before another flight, the position in the sequence is updated
// - When a flight is inserted, before another flight, the flight is inserted before the reference flight, and the reference flight and any trailing conflicts are delayed
// - When a flight is inserted, after another flight, the position in the sequence is updated
// - When a flight is inserted, after another flight, the flight is inserted behind the reference flight, and any trailing conflicts are delayed
// - When a flight is inserted, between two frozen flights, without enough space between them (2x landing rate), an exception is thrown

public class InsertFlightRequestHandler(ISessionManager sessionManager, IMediator mediator)
    : IRequestHandler<InsertFlightRequest>
{
    const int MaxCallsignLength = 12; // TODO: Verify the VATSIM limit

    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;

        var callsign = request.Callsign?.ToUpperInvariant().Truncate(MaxCallsignLength) ?? sequence.NewDummyCallsign();
        var state = State.Frozen; // TODO: Make this configurable

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
                sequence.InsertDummyFlight(
                    callsign,
                    request.AircraftType ?? string.Empty,
                    exactInsertionOptions.TargetLandingTime,
                    exactInsertionOptions.RunwayIdentifiers,
                    state);
                break;
            case RelativeInsertionOptions relativeInsertionOptions:
                sequence.InsertDummyFlight(
                    callsign,
                    request.AircraftType ?? string.Empty,
                    relativeInsertionOptions.Position,
                    relativeInsertionOptions.ReferenceCallsign,
                    state);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
