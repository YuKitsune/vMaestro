using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class DesequenceRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<DesequenceRequest>
{
    public async Task Handle(DesequenceRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying DesequenceRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var flight = sequence.FindFlight(request.Callsign);
            if (flight is null)
            {
                throw new MaestroException($"{request.Callsign} not found");
            }

            instance.Session.DeSequencedFlights.Add(flight);
            sequence.Remove(flight);

            logger.Information("Flight {Callsign} desequenced for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
