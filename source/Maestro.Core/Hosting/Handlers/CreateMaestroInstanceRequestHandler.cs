using Maestro.Core.Hosting.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Hosting.Handlers;

public class CreateMaestroInstanceRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<CreateMaestroInstanceRequest>
{
    public async Task Handle(CreateMaestroInstanceRequest request, CancellationToken cancellationToken)
    {
        await instanceManager.CreateInstance(request.AirportIdentifier, cancellationToken);

        logger.Information("Instance for {AirportIdentifier} created", request.AirportIdentifier);

        await mediator.Publish(new MaestroInstanceCreatedNotification(request.AirportIdentifier), cancellationToken);
    }
}
