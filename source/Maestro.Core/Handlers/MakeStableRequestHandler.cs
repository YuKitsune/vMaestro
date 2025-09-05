using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class MakeStableRequestHandler(ISequenceProvider sequenceProvider, IClock clock, IScheduler scheduler, IMediator mediator, ILogger logger)
    : IRequestHandler<MakeStableRequest>
{
    public async Task Handle(MakeStableRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return;
        }

        if (flight.State is not State.Unstable)
            return;

        flight.SetState(State.Stable, clock);
        scheduler.Schedule(lockedSequence.Sequence);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                lockedSequence.Sequence.AirportIdentifier,
                lockedSequence.Sequence.ToMessage()),
            cancellationToken);
    }
}
