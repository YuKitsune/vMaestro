using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public class ChangeRunwayRequestHandler(ISequenceProvider sequenceProvider, ILogger<RecomputeRequestHandler> logger)
    : IRequestHandler<ChangeRunwayRequest, ChangeRunwayResponse>
{
    public async Task<ChangeRunwayResponse> Handle(ChangeRunwayRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.LogWarning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new ChangeRunwayResponse();
        }
        
        var flight = await sequence.TryGetFlight(request.Callsign, cancellationToken);
        if (flight == null)
        {
            logger.LogWarning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new ChangeRunwayResponse();
        }
        
        flight.SetRunway(request.RunwayIdentifier);
        
        // TODO: Publish something to notify that the runway has changed.
        
        return new ChangeRunwayResponse();
    }
}