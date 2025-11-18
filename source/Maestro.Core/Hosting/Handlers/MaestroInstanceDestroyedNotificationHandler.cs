using Maestro.Core.Connectivity;
using Maestro.Core.Hosting.Contracts;
using MediatR;

namespace Maestro.Core.Hosting.Handlers;

public class MaestroInstanceDestroyedNotificationHandler(IMaestroConnectionManager connectionManager)
    : INotificationHandler<MaestroInstanceDestroyedNotification>
{
    public async Task Handle(MaestroInstanceDestroyedNotification notification, CancellationToken cancellationToken)
    {
        await connectionManager.RemoveConnection(notification.AirportIdentifier, cancellationToken);
    }
}
