using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

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

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
                sequence.MoveFlight(landedFlight.Callsign, exactInsertionOptions.TargetLandingTime, exactInsertionOptions.RunwayIdentifiers);
                break;
            case RelativeInsertionOptions relativeInsertionOptions:
                sequence.Reposition(landedFlight, relativeInsertionOptions.Position, relativeInsertionOptions.ReferenceCallsign);
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
