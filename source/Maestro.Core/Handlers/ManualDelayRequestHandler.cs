using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class ManualDelayRequestHandler(ISessionManager sessionManager, IMediator mediator)
    : IRequestHandler<ManualDelayRequest>
{
    public async Task Handle(ManualDelayRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            throw new MaestroException($"{request.Callsign} not found");
        }

        var maximumDelay = TimeSpan.FromMinutes(request.MaximumDelayMinutes);
        flight.SetMaximumDelay(maximumDelay);

        sequence.Recompute(flight);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
