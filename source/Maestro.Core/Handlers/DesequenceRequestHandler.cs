using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class DesequenceRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator, ILogger logger)
    : IRequestHandler<DesequenceRequest, DesequenceResponse>
{
    public async Task<DesequenceResponse> Handle(DesequenceRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.Warning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new DesequenceResponse();
        }

        var flight = await sequence.TryGetFlight(request.Callsign, cancellationToken);
        if (flight is null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new DesequenceResponse();
        }

        flight.Desequence();
        
        await mediator.Publish(new MaestroFlightUpdatedNotification(flight), cancellationToken);
        
        // TODO: Re-calculate sequence
        
        return new DesequenceResponse();
    }
}