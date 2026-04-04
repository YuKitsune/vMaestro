using Maestro.Contracts.Sessions;
using Maestro.Contracts.Slots;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ModifySlotRequestHandler(
    IMaestroInstanceManager instanceManager,
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

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;
            sequence.ModifySlot(request.SlotId, request.StartTime, request.EndTime);

            logger.Information("Slot {SlotId} modified for {AirportIdentifier} from {StartTime} to {EndTime}", request.SlotId, request.AirportIdentifier, request.StartTime, request.EndTime);

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
