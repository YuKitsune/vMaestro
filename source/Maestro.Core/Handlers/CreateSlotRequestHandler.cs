using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class CreateSlotRequestHandler(ISequenceProvider sequenceProvider, IScheduler scheduler, IMediator mediator) : IRequestHandler<CreateSlotRequest>
{
    public async Task Handle(CreateSlotRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        lockedSequence.Sequence.CreateSlot(request.StartTime, request.EndTime, request.RunwayIdentifiers, scheduler);
        await mediator.Publish(
            new SequenceUpdatedNotification(lockedSequence.Sequence.AirportIdentifier, lockedSequence.Sequence.ToMessage()),
            cancellationToken);
    }
}
