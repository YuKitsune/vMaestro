using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class MakePendingRequestHandler(
    ISessionManager sessionManager,
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
            logger.Information("Relaying MakePendingRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Making {Callsign} pending for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;
            var flight = sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            if (!flight.IsFromDepartureAirport)
                throw new MaestroException($"{flight.Callsign} is not from a departure airport.");

            sequence.Remove(flight);
            session.PendingFlights.Add(new PendingFlight(
                flight.Callsign,
                flight.IsFromDepartureAirport,
                flight.HighPriority));

            logger.Information("Marked flight {Callsign} as pending for {AirportIdentifier}", flight.Callsign, request.AirportIdentifier);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
