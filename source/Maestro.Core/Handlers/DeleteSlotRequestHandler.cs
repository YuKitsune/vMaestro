using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class DeleteSlotRequestHandler(ISessionManager sessionManager, IScheduler scheduler, IMediator mediator)
    : IRequestHandler<DeleteSlotRequest>
{
    public async Task Handle(DeleteSlotRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        sequence.DeleteSlot(request.SlotId);
        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
