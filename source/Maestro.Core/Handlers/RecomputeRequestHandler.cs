using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RecomputeRequestHandler(
    ISequenceProvider sequenceProvider,
    IScheduler scheduler,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<RecomputeRequest, RecomputeResponse>
{
    public async Task<RecomputeResponse> Handle(RecomputeRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RecomputeResponse();
        }

        flight.NeedsRecompute = true;
        scheduler.Schedule(lockedSequence.Sequence);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                lockedSequence.Sequence.AirportIdentifier,
                lockedSequence.Sequence.ToMessage()),
            cancellationToken);

        return new RecomputeResponse();
    }
}
