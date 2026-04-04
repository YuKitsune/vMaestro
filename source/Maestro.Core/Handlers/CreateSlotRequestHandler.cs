using Maestro.Contracts.Sessions;
using Maestro.Contracts.Slots;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class CreateSlotRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<CreateSlotRequest>
{
    public async Task Handle(CreateSlotRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying CreateSlotRequest for {AirportIdentifier} from {StartTime:HHmm} to {EndTime:HHmm}", request.AirportIdentifier, request.StartTime, request.EndTime);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Creating slot for {AirportIdentifier} from {StartTime:HHmm} to {EndTime:HHmm}", request.AirportIdentifier, request.StartTime, request.EndTime);

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var slotId = sequence.CreateSlot(request.StartTime, request.EndTime, request.RunwayIdentifiers);

            logger.Information(
                "Slot {SlotId} created for {AirportIdentifier} from {StartTime} to {EndTime}",
                slotId,
                request.AirportIdentifier,
                request.StartTime,
                request.EndTime);

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
