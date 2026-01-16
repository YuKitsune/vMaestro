using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ManualDelayRequestHandler(
    IMaestroInstanceManager instanceManager,
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

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;
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

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
