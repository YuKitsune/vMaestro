using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Integration;
using Maestro.Core.Messages;
using MediatR;
using Serilog;

namespace Maestro.Core.Connectivity.Handlers;

public class CreateConnectionRequestHandler(
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<CreateConnectionRequest>
{
    public async Task Handle(CreateConnectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await connectionManager.CreateConnection(
                request.AirportIdentifier,
                request.Partition,
                cancellationToken);

            await mediator.Publish(new ConnectionCreatedNotification(request.AirportIdentifier), cancellationToken);

            logger.Information("Connection for {AirportIdentifier} created", request.AirportIdentifier);

            // If connected to thet network, start the connection immediately
            var networkStatus = await TryGetNetworkStatusResponse(cancellationToken);
            if (networkStatus is not null && networkStatus.IsConnected)
            {
                await connection.Start(networkStatus.Position, cancellationToken);
                logger.Information("Connection for {AirportIdentifier} started", request.AirportIdentifier);

                await mediator.Publish(new ConnectionStartedNotification(request.AirportIdentifier), cancellationToken);
            }
        }
        catch (Exception e)
        {
            await mediator.Publish(new ErrorNotification(e), cancellationToken);
        }
    }

    async Task<GetNetworkStatusResponse?> TryGetNetworkStatusResponse(CancellationToken cancellationToken)
    {
        try
        {
            return await mediator.Send(new GetNetworkStatusRequest(),cancellationToken);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to get network status");
            return null;
        }
    }
}
