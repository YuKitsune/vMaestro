using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Sessions.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions.Handlers;

public class TrySwapRunwayModesRequestHandler(
    IMaestroConnectionManager connectionManager,
    ISessionManager sessionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<TrySwapRunwayModesRequest>
{
    public async Task Handle(TrySwapRunwayModesRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Debug("Skipping TrySwapRunwayMode for {AirportIdentifier} as we are not the master of this sequence", request.AirportIdentifier);
            return;
        }

        logger.Verbose("Attempting to swap runway modes for {AirportIdentifier}", request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            if (!session.Sequence.TrySwapRunwayModes())
            {
                logger.Debug("Did not swap runway modes for {AirportIdentifier}", request.AirportIdentifier);
                return;
            }

            logger.Information(
                "Runway mode for {AirportIdentifier} swapped to {CurrentRunwayMode}",
                session.AirportIdentifier,
                session.Sequence.CurrentRunwayMode.Identifier);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
