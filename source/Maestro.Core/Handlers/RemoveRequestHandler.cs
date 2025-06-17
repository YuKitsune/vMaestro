using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RemoveRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator, ILogger logger)
    : IRequestHandler<RemoveRequest, RemoveResponse>
{
    public async Task<RemoveResponse> Handle(RemoveRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        
        var flight = lockedSequence.Sequence.TryGetFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RemoveResponse();
        }
        
        flight.Remove();
        
        await mediator.Publish(
            new MaestroFlightUpdatedNotification(flight.ToMessage(lockedSequence.Sequence)),
            cancellationToken);
        
        // TODO: Re-calculate sequence
        
        return new RemoveResponse();
    }
}