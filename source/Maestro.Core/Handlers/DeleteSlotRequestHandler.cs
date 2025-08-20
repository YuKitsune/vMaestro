using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class DeleteSlotRequestHandler(ISequenceProvider sequenceProvider, IScheduler scheduler, IMediator mediator) : IRequestHandler<DeleteSlotRequest>
{
    public async Task Handle(DeleteSlotRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        lockedSequence.Sequence.DeleteSlot(request.SlotId, scheduler);
        await mediator.Publish(
            new SequenceUpdatedNotification(lockedSequence.Sequence.AirportIdentifier, lockedSequence.Sequence.ToMessage()),
            cancellationToken);
    }
}
