using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public class ZeroDelayRequestHandler(ISequenceProvider sequenceProvider, ILogger<RecomputeRequestHandler> logger)
    : IRequestHandler<ZeroDelayRequest, ZeroDelayResponse>
{
    public async Task<ZeroDelayResponse> Handle(ZeroDelayRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.LogWarning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new ZeroDelayResponse();
        }
        
        var flight = await sequence.TryGetFlight(request.Callsign, cancellationToken);
        if (flight == null)
        {
            logger.LogWarning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new ZeroDelayResponse();
        }

        throw new NotImplementedException("Make Pending not yet implemented.");
    }
}