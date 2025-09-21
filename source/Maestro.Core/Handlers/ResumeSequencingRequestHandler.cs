using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(ISessionManager sessionManager, IMediator mediator)
    : IRequestHandler<ResumeSequencingRequest>
{
    public async Task Handle(ResumeSequencingRequest request, CancellationToken cancellationToken)
    {
        using (var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken))
        {
            if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
            {
                await lockedSession.Session.Connection.Invoke(request, cancellationToken);
                return;
            }

            var sequence = lockedSession.Session.Sequence;
            sequence.Resume(request.Callsign);
        }

        // Let the RecomputeRequestHandler do the scheduling and notification
        await mediator.Send(new RecomputeRequest(request.AirportIdentifier, request.Callsign), cancellationToken);
    }
}
