using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
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
            logger.Information("Relaying ResumeSequencingRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Resuming sequencing for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;

            var flight = session.DeSequencedFlights.SingleOrDefault(f => f.Callsign == request.Callsign);
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

            sequence.Insert(Math.Max(earliestInsertionIndex, index), flight);
            session.DeSequencedFlights.Remove(flight);

            logger.Information("Flight {Callsign} resumed for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
