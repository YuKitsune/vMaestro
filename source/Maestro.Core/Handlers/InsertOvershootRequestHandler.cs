using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

// TODO: Test cases
// - When a flight is inserted, the state is set
// - When a flight is inserted, and it does not exist in the overshoot list, an exception is thrown
// - When a flight is inserted, with exact insertion options, the position in the sequence is updated
// - When a flight is inserted, with exact insertion options, the landing time and runway are updated
// - When a flight is inserted, before another flight, the position in the sequence is updated
// - When a flight is inserted, before another flight, the flight is inserted before the reference flight, and the reference flight and any trailing conflicts are delayed
// - When a flight is inserted, after another flight, the position in the sequence is updated
// - When a flight is inserted, after another flight, the flight is inserted behind the reference flight, and any trailing conflicts are delayed
// - When a flight is inserted, between two frozen flights, without enough space between them (2x landing rate), an exception is thrown

public class InsertOvershootRequestHandler(
    ISessionManager sessionManager,
    IClock clock,
    IMediator mediator)
    : IRequestHandler<InsertOvershootRequest>
{
    public async Task Handle(InsertOvershootRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;

        // BUG: If inserting after a frozen flight, nothing happens
        var landedFlight = sequence.FindTrackedFlight(request.Callsign);
        if (landedFlight is null)
        {
            throw new MaestroException($"Flight {request.Callsign} not found in landed flights");
        }

        sequence.Remove(landedFlight.Callsign);

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
                sequence.Insert(landedFlight, exactInsertionOptions.TargetLandingTime);
                break;
            case RelativeInsertionOptions relativeInsertionOptions:
                sequence.Insert(landedFlight, relativeInsertionOptions.Position, relativeInsertionOptions.ReferenceCallsign);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // TODO: Validate flights cannot be inserted between frozen flights when there is less than 2x the acceptance rate

        landedFlight.SetState(State.Frozen, clock);
        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
