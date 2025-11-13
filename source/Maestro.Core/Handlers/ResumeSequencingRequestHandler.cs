using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
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

        var runwayMode = sequence.GetRunwayModeAt(flight.LandingEstimate);
        var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier) ??
                     runwayMode.Default;

        // TODO: If the runway mode has changed since the flight was desequenced, re-assign the runway based on the flights feeder fix

        // Don't insert the flight in front of any SuperStable, Frozen, or Landed flights
        var earliestInsertionIndex = sequence.FindLastIndex(f =>
            f.State is not State.Unstable and not State.Stable &&
            f.AssignedRunwayIdentifier == runway.Identifier) + 1;

        var index = sequence.FindIndex(
            earliestInsertionIndex,
            f => f.LandingEstimate.IsBefore(flight.LandingEstimate)) + 1;

        sequence.Insert(index, flight);
        lockedSession.Session.DeSequencedFlights.Remove(flight);

        logger.Information("Flight {Callsign} resumed for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

        await mediator.Publish(new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()), cancellationToken);
    }
}
