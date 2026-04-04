using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RemoveRequestHandler(
    ISessionManager sessionManager,
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
            logger.Information("Relaying RemoveRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Removing {Callsign} for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;

            var sequencedFlight = sequence.FindFlight(request.Callsign);
            if (sequencedFlight is not null)
            {
                sequence.Remove(sequencedFlight);

                if (!sequencedFlight.IsManuallyInserted)
                    session.PendingFlights.Add(new PendingFlight(
                        sequencedFlight.Callsign,
                        sequencedFlight.IsFromDepartureAirport,
                        sequencedFlight.HighPriority));
            }

            var desequencedFlight = session.DeSequencedFlights.SingleOrDefault(f => f.Callsign == request.Callsign);
            if (desequencedFlight is not null)
            {
                session.DeSequencedFlights.Remove(desequencedFlight);

                if (!desequencedFlight.IsManuallyInserted)
                    session.PendingFlights.Add(new PendingFlight(
                        desequencedFlight.Callsign,
                        desequencedFlight.IsFromDepartureAirport,
                        desequencedFlight.HighPriority));
            }

            if (sequencedFlight is null && desequencedFlight is null)
            {
                throw new MaestroException($"{request.Callsign} not found");
            }

            logger.Information("{Callsign} removed for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
