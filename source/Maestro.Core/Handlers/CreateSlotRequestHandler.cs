using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class CreateSlotRequestHandler(ISessionManager sessionManager, IMediator mediator, ILogger logger)
    : IRequestHandler<CreateSlotRequest>
{
    public async Task Handle(CreateSlotRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            logger.Information("Relaying CreateSlotRequest for {AirportIdentifier}", request.AirportIdentifier);
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var slotId =sequence.CreateSlot(request.StartTime, request.EndTime, request.RunwayIdentifiers);

        logger.Information(
            "Slot {SlotId} created for {AirportIdentifier} from {StartTime} to {EndTime}",
            slotId,
            request.AirportIdentifier,
            request.StartTime,
            request.EndTime);

        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
