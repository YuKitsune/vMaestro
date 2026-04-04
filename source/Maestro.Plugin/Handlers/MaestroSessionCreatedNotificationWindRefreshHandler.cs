using Maestro.Core.Sessions.Contracts;
using MediatR;
using vatsys;

namespace Maestro.Plugin.Handlers;

/// <summary>
/// Updates the winds as soon as the instance is started, if already connected to VATSIM.
/// </summary>
public class MaestroSessionCreatedNotificationWindRefreshHandler(IMediator mediator)
    : INotificationHandler<MaestroSessionCreatedNotification>
{
    public async Task Handle(MaestroSessionCreatedNotification notification, CancellationToken cancellationToken)
    {
        if (!Network.IsConnected)
            return;

        await mediator.Send(
            new RefreshWindRequest(notification.AirportIdentifier),
            cancellationToken);
    }
}
