using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(ISequenceProvider sequenceProvider, IScheduler scheduler, IMediator mediator, ILogger logger)
    : IRequestHandler<ResumeSequencingRequest>
{
    public async Task Handle(ResumeSequencingRequest request, CancellationToken cancellationToken)
    {
        using (var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken))
        {
            var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
            if (flight is null)
            {
                logger.Warning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
                return;
            }

            flight.Resume();
        }

        // Let the RecomputeRequestHandler do the scheduling and notification
        await mediator.Send(new RecomputeRequest(request.AirportIdentifier, request.Callsign), cancellationToken);
    }
}
