using Maestro.Core.Hosting.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Hosting.Handlers;

public class DestroyMaestroInstanceRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<DestroyMaestroInstanceRequest>
{
    public async Task Handle(DestroyMaestroInstanceRequest request, CancellationToken cancellationToken)
    {
        await instanceManager.DestroyInstance(request.AirportIdentifier, cancellationToken);

        logger.Information("Instance for {AirportIdentifier} destroyed", request.AirportIdentifier);

        await mediator.Publish(
            new MaestroInstanceDestroyedNotification(request.AirportIdentifier),
            cancellationToken);
    }
}
