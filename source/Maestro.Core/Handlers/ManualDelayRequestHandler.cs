using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
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
            logger.Information("Relaying ManualDelayRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Setting maximum delay for {Callsign} to {MaximumDelayMinutes}min at {AirportIdentifier}", request.Callsign, request.MaximumDelayMinutes, request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;
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
            sequence.Schedule(index);

            logger.Information("Set maximum delay for {Callsign} to {MaximumDelay}", request.Callsign, maximumDelay);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
