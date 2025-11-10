using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Connectivity.Handlers;

public class CreateConnectionRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<CreateConnectionRequest>
{
    public async Task Handle(CreateConnectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);

            var connection = await connectionManager.CreateConnection(
                request.AirportIdentifier,
                request.Partition,
                cancellationToken);

            logger.Information("Connection for {AirportIdentifier} created", request.AirportIdentifier);

            // If the session is active, start the connection immediately
            if (lockedSession.Session.IsActive)
            {
                await connection.Start(lockedSession.Session.Position, cancellationToken);
                logger.Information("Connection for {AirportIdentifier} started", request.AirportIdentifier);
            }
        }
        catch (Exception e)
        {
            await mediator.Publish(new ErrorNotification(e), cancellationToken);
        }
    }
}
