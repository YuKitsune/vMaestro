using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator, ILogger<RecomputeRequestHandler> logger)
    : IRequestHandler<ResumeSequencingRequest, ResumeSequencingResponse>
{
    public async Task<ResumeSequencingResponse> Handle(ResumeSequencingRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.LogWarning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new ResumeSequencingResponse();
        }

        var flight = await sequence.TryGetFlight(request.Callsign, cancellationToken);
        if (flight is null)
        {
            logger.LogWarning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new ResumeSequencingResponse();
        }
        
        flight.Resume();
        
        await mediator.Send(new RecomputeRequest(request.AirportIdentifier, request.Callsign), cancellationToken);
        return new ResumeSequencingResponse();
    }
}