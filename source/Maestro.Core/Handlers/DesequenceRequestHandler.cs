using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class DesequenceRequestHandler(ISequenceProvider sequenceProvider, IScheduler scheduler, IMediator mediator, ILogger logger)
    : IRequestHandler<DesequenceRequest, DesequenceResponse>
{
    public async Task<DesequenceResponse> Handle(DesequenceRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
        if (flight is null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new DesequenceResponse();
        }

        flight.Desequence();
        scheduler.Schedule(lockedSequence.Sequence);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                lockedSequence.Sequence.AirportIdentifier,
                lockedSequence.Sequence.ToMessage()),
            cancellationToken);

        return new DesequenceResponse();
    }
}
