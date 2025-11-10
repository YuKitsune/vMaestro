using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Connectivity.Handlers;

public class DestroyConnectionRequestHandler(IMaestroConnectionManager connectionManager, IMediator mediator)
    : IRequestHandler<DestroyConnectionRequest>
{
    public async Task Handle(DestroyConnectionRequest request, CancellationToken cancellationToken)
    {
        await connectionManager.RemoveConnection(request.AirportIdentifier, cancellationToken);
        await mediator.Publish(new ConnectionDestroyedNotification(request.AirportIdentifier), cancellationToken);
    }
}
