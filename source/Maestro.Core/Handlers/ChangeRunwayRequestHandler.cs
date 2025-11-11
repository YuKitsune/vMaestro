using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeRunwayRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeRunwayRequest>
{
    public async Task Handle(ChangeRunwayRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ChangeRunwayRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSession.Session.Sequence;

        var flight = sequence.FindFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return;
        }

        // TODO: Track who initiated the change
        logger.Information("Changing runway for {Callsign} to {NewRunway}.", request.Callsign, request.RunwayIdentifier);

        flight.SetRunway(request.RunwayIdentifier, true);
        sequence.Recompute(flight);

        // Unstable flights become Stable when changing runway
        if (flight.State is State.Unstable)
            flight.SetState(State.Stable, clock);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
