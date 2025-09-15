using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(ISessionManager sessionManager, IScheduler scheduler, IMediator mediator, ILogger logger)
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
            var flight = sequence.FindTrackedFlight(request.Callsign);
            if (flight is null)
            {
                logger.Warning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
                return;
            }

            flight.Resume();
            scheduler.Schedule(sequence);
        }

        // Let the RecomputeRequestHandler do the scheduling and notification
        await mediator.Send(new RecomputeRequest(request.AirportIdentifier, request.Callsign), cancellationToken);
    }
}
