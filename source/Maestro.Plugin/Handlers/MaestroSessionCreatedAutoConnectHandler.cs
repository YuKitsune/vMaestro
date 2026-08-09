using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Sessions.Contracts;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class MaestroSessionCreatedAutoConnectHandler(
    ServerConfiguration serverConfiguration,
    IMaestroConnectionManager connectionManager,
    IMediator mediator)
    : INotificationHandler<MaestroSessionCreatedNotification>
{
    public async Task Handle(MaestroSessionCreatedNotification notification, CancellationToken cancellationToken)
    {
        if (!serverConfiguration.AutoConnect)
            return;

        if (connectionManager.TryGetConnection(notification.AirportIdentifier, out _))
            return;

        await mediator.Send(
            new CreateConnectionRequest(
                notification.AirportIdentifier,
                connectionManager.CurrentServerUrl,
                serverConfiguration.Environments.First()),
            cancellationToken);
    }
}
