using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class MakeStableRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<MakeStableRequest>
{
    public async Task Handle(MakeStableRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying MakeStableRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Making {Callsign} stable for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;
            var flight = sequence.FindFlight(request.Callsign);
            if (flight is null)
                return;

            if (flight.State is not State.Unstable)
                return;

            flight.SetState(State.Stable, clock);

            logger.Information("Flight {Callsign} made stable", flight.Callsign);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
