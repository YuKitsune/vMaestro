using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class InsertPendingRequestHandler(
    ISessionManager sessionManager,
    IClock clock,
    IMediator mediator)
    : IRequestHandler<InsertPendingRequest>
{
    public async Task Handle(InsertPendingRequest request, CancellationToken cancellationToken)
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

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
                sequence.InsertPending(flight.Callsign, exactInsertionOptions.TargetLandingTime, exactInsertionOptions.RunwayIdentifiers);
                break;
            case RelativeInsertionOptions relativeInsertionOptions:
                sequence.InsertPending(flight.Callsign, relativeInsertionOptions.Position, relativeInsertionOptions.ReferenceCallsign);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        flight.SetState(State.Stable, clock);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
