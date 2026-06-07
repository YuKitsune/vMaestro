using Maestro.Contracts.Coordination;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeLandingRatesRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeLandingRatesRequest>
{
    public async Task Handle(ChangeLandingRatesRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ChangeLandingRatesRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Changing landing rates for {AirportIdentifier}", request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            session.Sequence.ChangeLandingRates(request.NewLandingRates, request.ChangeTime);

            logger.Information(
                "Landing rates change scheduled for {AirportIdentifier} at {ChangeTime}.",
                request.AirportIdentifier,
                request.ChangeTime);

            await mediator.Send(
                new SendCoordinationMessageRequest(
                    request.AirportIdentifier,
                    clock.UtcNow(),
                    $"Landing rates change scheduled for {request.ChangeTime:HH:mm}",
                    new CoordinationDestination.Broadcast()),
                cancellationToken);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
