using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
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
            logger.Information("Relaying DesequenceRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Desequencing {Callsign} for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

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

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
