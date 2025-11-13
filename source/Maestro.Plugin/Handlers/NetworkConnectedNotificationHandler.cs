using Maestro.Core.Connectivity;
using Maestro.Core.Hosting;
using MediatR;
using Serilog;

namespace Maestro.Plugin.Handlers;

public record NetworkConnectedNotification(string PositionIdentifier) : INotification;

public class NetworkConnectedNotificationHandler(
    IMaestroConnectionManager connectionManager,
    IMaestroInstanceManager instanceManager,
    ILogger logger)
    : INotificationHandler<NetworkConnectedNotification>
{
    public async Task Handle(NetworkConnectedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var airportIdentifier in instanceManager.ActiveInstances)
        {
            if (!connectionManager.TryGetConnection(airportIdentifier, out var connection))
                continue;

            try
            {
                if (connection!.IsConnected)
                    continue;

                await connection.Start(notification.PositionIdentifier, cancellationToken);
            }
            catch (Exception e)
            {
                logger.Error(e, "Error starting connection for {AirportIdentifier}", airportIdentifier);
                throw;
            }
        }
    }
}
