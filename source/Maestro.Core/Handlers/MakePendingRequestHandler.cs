using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class MakePendingRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<MakePendingRequest>
{
    public async Task Handle(MakePendingRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying MakePendingRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;
            var flight = sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            if (!flight.IsFromDepartureAirport)
                throw new MaestroException($"{flight.Callsign} is not from a departure airport.");

            sequence.Remove(flight);
            instance.Session.PendingFlights.Add(new PendingFlight(
                flight.Callsign,
                flight.IsFromDepartureAirport,
                flight.HighPriority));

            logger.Information("Marked flight {Callsign} as pending for {AirportIdentifier}", flight.Callsign, request.AirportIdentifier);

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
