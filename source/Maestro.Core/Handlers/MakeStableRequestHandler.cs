using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

// TODO: Test cases
// - Unstable flight becomes stable
// - Flights in other modes are ignored
// - Flight does not become unstable when manually stablised

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
            logger.Information("Relaying MakeStableRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.FindFlight(request.Callsign);
        if (flight is null)
            return;

        if (flight.State is not State.Unstable)
            return;

        flight.SetState(State.Stable, clock);

        logger.Information("Flight {Callsign} made stable", flight.Callsign);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
