using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class InsertOvershootRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<InsertOvershootRequest>
{
    public async Task Handle(InsertOvershootRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying InsertOvershootRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSession.Session.Sequence;

        // BUG: If inserting after a frozen flight, nothing happens
        var landedFlight = sequence.FindFlight(request.Callsign);
        if (landedFlight is null)
        {
            throw new MaestroException($"Flight {request.Callsign} not found in landed flights");
        }

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
                sequence.MoveFlight(landedFlight.Callsign, exactInsertionOptions.TargetLandingTime, exactInsertionOptions.RunwayIdentifiers);
                break;
            case RelativeInsertionOptions relativeInsertionOptions:
                sequence.Reposition(landedFlight, relativeInsertionOptions.Position, relativeInsertionOptions.ReferenceCallsign);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // TODO: Validate flights cannot be inserted between frozen flights when there is less than 2x the acceptance rate

        landedFlight.SetState(State.Frozen, clock);

        logger.Information("Inserted overshoot flight {Callsign} for {AirportIdentifier}", landedFlight.Callsign, request.AirportIdentifier);

        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
