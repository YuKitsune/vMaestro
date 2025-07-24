using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(ISequenceProvider sequenceProvider, ISlotBasedScheduler scheduler, ILogger logger)
    : IRequestHandler<ResumeSequencingRequest, ResumeSequencingResponse>
{
    public async Task<ResumeSequencingResponse> Handle(ResumeSequencingRequest request, CancellationToken cancellationToken)
    {
        using (var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken))
        {
            var flight = lockedSequence.Sequence.FindFlight(request.Callsign);
            if (flight is null)
            {
                logger.Warning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
                return new ResumeSequencingResponse();
            }

            lockedSequence.Sequence.ResumeSequencing(flight.Callsign, scheduler);
            flight.NeedsRecompute = true;

            // TODO: Publish notification for flight resumed.
        }

        return new ResumeSequencingResponse();
    }
}
