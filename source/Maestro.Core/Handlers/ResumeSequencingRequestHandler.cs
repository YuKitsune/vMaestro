using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ResumeSequencingRequest>
{
    public async Task Handle(ResumeSequencingRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ResumeSequencingRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSession.Session.Sequence;

        var flight = lockedSession.Session.DeSequencedFlights.SingleOrDefault(f => f.Callsign == request.Callsign);
        if (flight is null)
            throw new MaestroException($"{request.Callsign} was not found in the desequenced list.");

        // Insert the flight based on its ETA relative to other flights
        var index = sequence.FirstIndexOf(f => f.LandingEstimate.IsBefore(flight.LandingEstimate)) + 1;

        sequence.Insert(index, flight);
        lockedSession.Session.DeSequencedFlights.Remove(flight);

        logger.Information("Flight {Callsign} resumed for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

        await mediator.Publish(new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()), cancellationToken);
    }
}
