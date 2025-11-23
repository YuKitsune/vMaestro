using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RemoveRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<RemoveRequest>
{
    public async Task Handle(RemoveRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying RemoveRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var sequencedFlight = sequence.FindFlight(request.Callsign);
            if (sequencedFlight is not null)
            {
                logger.Information("Removing sequenced flight {Callsign} from sequence for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
                sequence.Remove(sequencedFlight);

                sequencedFlight.Reset();
                instance.Session.PendingFlights.Add(sequencedFlight);
            }

            var desequencedFlight = instance.Session.DeSequencedFlights.SingleOrDefault(f => f.Callsign == request.Callsign);
            if (desequencedFlight is not null)
            {
                logger.Information("Removing desequenced flight {Callsign} from de-sequenced flights for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
                instance.Session.DeSequencedFlights.Remove(desequencedFlight);

                desequencedFlight.Reset();
                instance.Session.PendingFlights.Add(desequencedFlight);
            }

            if (sequencedFlight is null && desequencedFlight is null)
            {
                throw new MaestroException($"{request.Callsign} not found");
            }

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
