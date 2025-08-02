using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(ISequenceProvider sequenceProvider, IScheduler scheduler, IMediator mediator, ILogger logger)
    : IRequestHandler<ResumeSequencingRequest, ResumeSequencingResponse>
{
    public async Task<ResumeSequencingResponse> Handle(ResumeSequencingRequest request, CancellationToken cancellationToken)
    {
        using (var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken))
        {
            var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
            if (flight is null)
            {
                logger.Warning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
                return new ResumeSequencingResponse();
            }

            flight.Resume();
            flight.NeedsRecompute = true;

            scheduler.Schedule(lockedSequence.Sequence);

            await mediator.Publish(
                new SequenceUpdatedNotification(
                    lockedSequence.Sequence.AirportIdentifier,
                    lockedSequence.Sequence.ToMessage()),
                cancellationToken);
        }

        return new ResumeSequencingResponse();
    }
}
