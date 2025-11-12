using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class DesequenceRequestHandler(
    ISessionManager sessionManager,
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

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSession.Session.Sequence;

        var flight = sequence.FindFlight(request.Callsign);
        if (flight is null)
        {
            throw new MaestroException($"{request.Callsign} not found");
        }

        lockedSession.Session.DeSequencedFlights.Add(flight);
        sequence.Remove(flight);

        logger.Information("Flight {Callsign} desequenced for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
