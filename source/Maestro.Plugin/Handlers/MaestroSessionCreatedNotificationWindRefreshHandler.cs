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
    public Task Handle(MaestroSessionCreatedNotification notification, CancellationToken cancellationToken)
    {
        if (!Network.IsConnected)
            return Task.CompletedTask;

        // Fire-and-forget: awaiting blocks the channel publisher from processing subsequent notifications
        // (e.g. NetworkConnectedNotification) until the wind timeout expires, leaving the window blank.
        _ = mediator.Send(new RefreshWindRequest(notification.AirportIdentifier), cancellationToken);
        return Task.CompletedTask;
    }
}
