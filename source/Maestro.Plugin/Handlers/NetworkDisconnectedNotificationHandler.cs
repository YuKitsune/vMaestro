using Maestro.Core.Connectivity;
using Maestro.Core.Hosting;
using MediatR;
using Serilog;

namespace Maestro.Plugin.Handlers;

public record NetworkDisconnectedNotification : INotification;

public class NetworkDisconnectedNotificationHandler(
    IMaestroConnectionManager connectionManager,
    IMaestroInstanceManager instanceManager,
    ILogger logger)
    : INotificationHandler<NetworkDisconnectedNotification>
{
    public async Task Handle(NetworkDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var airportIdentifier in instanceManager.ActiveInstances)
        {
            if (!connectionManager.TryGetConnection(airportIdentifier, out var connection))
                continue;

            try
            {
                if (!connection!.IsConnected)
                    continue;

                await connection.Stop(cancellationToken);
            }
            catch (Exception e)
            {
                logger.Error(e, "Error stopping connection for {AirportIdentifier}", airportIdentifier);
                throw;
            }
        }
    }
}
