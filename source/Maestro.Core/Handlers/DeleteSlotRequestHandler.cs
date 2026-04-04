using Maestro.Contracts.Sessions;
using Maestro.Contracts.Slots;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class DeleteSlotRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<DeleteSlotRequest>
{
    public async Task Handle(DeleteSlotRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying DeleteSlotRequest for slot {SlotId} at {AirportIdentifier}", request.SlotId, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Deleting slot {SlotId} for {AirportIdentifier}", request.SlotId, request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;

            sequence.DeleteSlot(request.SlotId);

            logger.Information("Slot {SlotId} deleted for {AirportIdentifier}", request.SlotId, request.AirportIdentifier);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
