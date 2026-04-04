using Maestro.Contracts.Sessions;
using Maestro.Contracts.Slots;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ModifySlotRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ModifySlotRequest>
{
    public async Task Handle(ModifySlotRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ModifySlotRequest for slot {SlotId} at {AirportIdentifier}", request.SlotId, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Modifying slot {SlotId} for {AirportIdentifier}", request.SlotId, request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;
            sequence.ModifySlot(request.SlotId, request.StartTime, request.EndTime);

            logger.Information("Slot {SlotId} modified for {AirportIdentifier} from {StartTime} to {EndTime}", request.SlotId, request.AirportIdentifier, request.StartTime, request.EndTime);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
