using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public class RecomputeRequestHandler(ISequenceProvider sequenceProvider, ILogger<RecomputeRequestHandler> logger)
    : IRequestHandler<RecomputeRequest, RecomputeResponse>
{
    public async Task<RecomputeResponse> Handle(RecomputeRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.LogWarning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new RecomputeResponse();
        }
        
        var flight = await sequence.TryGetFlight(request.Callsign, cancellationToken);
        if (flight == null)
        {
            logger.LogWarning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RecomputeResponse();
        }

        throw new NotImplementedException("Recompute not yet implemented.");
    }
}