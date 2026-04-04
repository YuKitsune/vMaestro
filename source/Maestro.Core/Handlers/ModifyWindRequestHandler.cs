using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ModifyWindRequestHandler(
    IMaestroInstanceManager instanceManager,
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

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            instance.Session.Sequence.SurfaceWind = new Wind(request.SurfaceWind.Direction, request.SurfaceWind.Speed);
            instance.Session.Sequence.UpperWind = new Wind(request.UpperWind.Direction, request.UpperWind.Speed);
            instance.Session.Sequence.ManualWind = request.ManualWind;

            logger.Information(
                "Wind modified for {AirportIdentifier}: Surface {SurfaceWind}, Upper {UpperWind}, Manual={ManualWind}",
                request.AirportIdentifier,
                $"{request.SurfaceWind.Direction:000}/{request.SurfaceWind.Speed:000}",
                $"{request.UpperWind.Direction:000}/{request.UpperWind.Speed:000}",
                request.ManualWind);

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
