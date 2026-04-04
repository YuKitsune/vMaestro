using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ModifyWindRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ModifyWindRequest>
{
    public async Task Handle(ModifyWindRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            // Slaves may only relay manual wind updates
            if (!request.ManualWind)
            {
                logger.Verbose("Ignoring automatic wind update for {AirportIdentifier} - slave connections may only relay manual updates", request.AirportIdentifier);
                return;
            }

            logger.Information("Relaying ModifyWindRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Modifying wind for {AirportIdentifier}", request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            session.Sequence.SurfaceWind = new Wind(request.SurfaceWind.Direction, request.SurfaceWind.Speed);
            session.Sequence.UpperWind = new Wind(request.UpperWind.Direction, request.UpperWind.Speed);
            session.Sequence.ManualWind = request.ManualWind;

            logger.Information(
                "Wind modified for {AirportIdentifier}: Surface {SurfaceWind}, Upper {UpperWind}, Manual={ManualWind}",
                request.AirportIdentifier,
                $"{request.SurfaceWind.Direction:000}/{request.SurfaceWind.Speed:000}",
                $"{request.UpperWind.Direction:000}/{request.UpperWind.Speed:000}",
                request.ManualWind);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
