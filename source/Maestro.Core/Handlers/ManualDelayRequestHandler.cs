using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ManualDelayRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ManualDelayRequest>
{
    public async Task Handle(ManualDelayRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ManualDelayRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.FindFlight(request.Callsign);
        if (flight == null)
            throw new MaestroException($"{request.Callsign} not found");

        var index = sequence.IndexOf(flight);
        if (index < 0)
            throw new MaestroException($"{request.Callsign} not found");

        var maximumDelay = TimeSpan.FromMinutes(request.MaximumDelayMinutes);
        flight.SetMaximumDelay(maximumDelay);

        // Re-schedule the flight
        // This will move it forward if the delay exceeds the maximum delay
        // TODO: Should this be a different method?
        sequence.Schedule(index, forceRescheduleStable: true);

        logger.Information("Set maximum delay for {Callsign} to {MaximumDelay}", request.Callsign, maximumDelay);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
