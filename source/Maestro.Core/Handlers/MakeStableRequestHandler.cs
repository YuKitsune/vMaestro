using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

// TODO: Test cases
// - Unstable flight becomes stable
// - Flights in other modes are ignored
// - Flight does not become unstable when manually stablised

public class MakeStableRequestHandler(ISessionManager sessionManager, IClock clock, IScheduler scheduler, IMediator mediator)
    : IRequestHandler<MakeStableRequest>
{
    public async Task Handle(MakeStableRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Send(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.FindTrackedFlight(request.Callsign);
        if (flight is null)
            return;

        if (flight.State is not State.Unstable)
            return;

        flight.SetState(State.Stable, clock);
        scheduler.Schedule(sequence);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
