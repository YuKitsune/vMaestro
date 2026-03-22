using Maestro.Core.Hosting.Contracts;
using MediatR;
using vatsys;

namespace Maestro.Plugin.Handlers;

/// <summary>
/// Updates the winds as soon as the instance is started, if already connected to VATSIM.
/// </summary>
public class MaestroInstanceCreatedNotificationWindRefreshHandler(IMediator mediator)
    : INotificationHandler<MaestroInstanceCreatedNotification>
{
    public async Task Handle(MaestroInstanceCreatedNotification notification, CancellationToken cancellationToken)
    {
        if (!Network.IsConnected)
            return;

        await mediator.Send(
            new RefreshWindRequest(notification.AirportIdentifier),
            cancellationToken);
    }
}
