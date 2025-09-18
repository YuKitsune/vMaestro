using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class MoveFlightRequestHandler(
    ISessionManager sessionManager,
    IMediator mediator)
    : IRequestHandler<MoveFlightRequest>
{
    public async Task Handle(MoveFlightRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.FindTrackedFlight(request.Callsign);
        if (flight is null)
            return;

        // Delegate to Sequence aggregate to handle the move operation
        sequence.MoveFlight(
            request.Callsign,
            request.NewLandingTime,
            request.RunwayIdentifiers);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
