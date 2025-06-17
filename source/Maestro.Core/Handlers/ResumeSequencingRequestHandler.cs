using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator, ILogger logger)
    : IRequestHandler<ResumeSequencingRequest, ResumeSequencingResponse>
{
    public async Task<ResumeSequencingResponse> Handle(ResumeSequencingRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.TryGetFlight(request.Callsign);
        if (flight is null)
        {
            logger.Warning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new ResumeSequencingResponse();
        }
        
        flight.Resume();
        
        await mediator.Send(new RecomputeRequest(request.AirportIdentifier, request.Callsign), cancellationToken);
        return new ResumeSequencingResponse();
    }
}