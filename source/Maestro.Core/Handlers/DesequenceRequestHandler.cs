using Maestro.Core.Extensions;
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
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        
        var flight = lockedSequence.Sequence.TryGetFlight(request.Callsign);
        if (flight is null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new DesequenceResponse();
        }

        flight.Desequence();

        await mediator.Publish(
            new MaestroFlightUpdatedNotification(
                flight.ToMessage(lockedSequence.Sequence)),
            cancellationToken);
        
        // TODO: Re-calculate sequence
        
        return new DesequenceResponse();
    }
}