using Maestro.Core.Connectivity.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Connectivity.Handlers;

public class StopConnectionRequestRequestHandler(IMaestroConnectionManager connectionManager, IMediator mediator, ILogger logger)
    : IRequestHandler<StopConnectionRequest>
{
    public async Task Handle(StopConnectionRequest request, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(request.AirportIdentifier, out var connection))
            return;

        await connection!.Stop(cancellationToken);
        logger.Information("Disconnected session for {AirportIdentifier}", request.AirportIdentifier);

        await mediator.Publish(new ConnectionStoppedNotification(request.AirportIdentifier), cancellationToken);
    }
}
