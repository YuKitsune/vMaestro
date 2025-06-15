using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RecomputeRequestHandler(
    ISequenceProvider sequenceProvider,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<RecomputeRequest, RecomputeResponse>
{
    public async Task<RecomputeResponse> Handle(RecomputeRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.Warning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new RecomputeResponse();
        }
        
        var flight = await sequence.TryGetFlight(request.Callsign, cancellationToken);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RecomputeResponse();
        }

        flight.NeedsRecompute = true;
        
        await mediator.Publish(new MaestroFlightUpdatedNotification(flight), cancellationToken);
        return new RecomputeResponse();
    }
}