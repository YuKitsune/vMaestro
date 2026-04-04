using Maestro.Core.Connectivity;
using Maestro.Core.Sessions.Contracts;
using MediatR;

namespace Maestro.Core.Sessions.Handlers;

public class MaestroSessionDestroyedNotificationHandler(IMaestroConnectionManager connectionManager)
    : INotificationHandler<MaestroSessionDestroyedNotification>
{
    public async Task Handle(MaestroSessionDestroyedNotification notification, CancellationToken cancellationToken)
    {
        await connectionManager.RemoveConnection(notification.AirportIdentifier, cancellationToken);
    }
}
