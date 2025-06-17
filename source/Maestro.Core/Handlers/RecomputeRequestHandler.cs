using Maestro.Core.Extensions;
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
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        
        var flight = lockedSequence.Sequence.TryGetFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RecomputeResponse();
        }

        flight.NeedsRecompute = true;
        
        await mediator.Publish(
            new MaestroFlightUpdatedNotification(flight.ToMessage(lockedSequence.Sequence)),
            cancellationToken);
        return new RecomputeResponse();
    }
}