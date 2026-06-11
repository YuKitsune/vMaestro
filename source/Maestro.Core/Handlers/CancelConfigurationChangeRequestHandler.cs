using Maestro.Contracts.Connectivity;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class CancelConfigurationChangeRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<CancelRunwayModeChangeRequest>,
        IRequestHandler<CancelLandingRatesChangeRequest>
{
    public Task Handle(CancelRunwayModeChangeRequest request, CancellationToken cancellationToken) =>
        Cancel(request, request.AirportIdentifier, cancellationToken);

    public Task Handle(CancelLandingRatesChangeRequest request, CancellationToken cancellationToken) =>
        Cancel(request, request.AirportIdentifier, cancellationToken);

    async Task Cancel<TRequest>(TRequest request, string airportIdentifier, CancellationToken cancellationToken)
        where TRequest : class, IRelayableRequest, IRequest
    {
        if (connectionManager.TryGetConnection(airportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Debug("Relaying {RequestType} for {AirportIdentifier}", typeof(TRequest).Name, airportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Cancelling configuration change for {AirportIdentifier}", airportIdentifier);

        var session = await sessionManager.GetSession(airportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            if (session.Sequence.PendingConfigurationChange is null)
            {
                logger.Warning("Attempted to cancel configuration change for {AirportIdentifier} but no change was pending", airportIdentifier);
                return;
            }

            session.Sequence.CancelConfigurationChange();

            logger.Information("{AirportIdentifier} configuration change cancelled", airportIdentifier);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                airportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
