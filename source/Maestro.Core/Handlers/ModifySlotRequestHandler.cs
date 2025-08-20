using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class ModifySlotRequestHandler(ISequenceProvider sequenceProvider, IScheduler scheduler, IMediator mediator) : IRequestHandler<ModifySlotRequest>
{
    public async Task Handle(ModifySlotRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        lockedSequence.Sequence.ModifySlot(request.SlotId, request.StartTime, request.EndTime, scheduler);
        await mediator.Publish(
            new SequenceUpdatedNotification(lockedSequence.Sequence.AirportIdentifier, lockedSequence.Sequence.ToMessage()),
            cancellationToken);
    }
}
