using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeRunwayRequestHandler(ISessionManager sessionManager, IArrivalLookup arrivalLookup, IClock clock, IMediator mediator, ILogger logger)
    : IRequestHandler<ChangeRunwayRequest>
{
    public async Task Handle(ChangeRunwayRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return;
        }

        // TODO: These changes need to be atomic.
        flight.SetRunway(request.Runway.Identifier, true);
        flight.ChangeApproachType(request.Runway.ApproachType);
        RecomputeEstimates(flight);

        sequence.Recompute(flight);

        // Unstable flights become Stable when changing runway
        if (flight.State is State.Unstable)
            flight.SetState(State.Stable, clock);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }

    void RecomputeEstimates(Flight flight)
    {
        if (string.IsNullOrEmpty(flight.FeederFixIdentifier))
            return;

        var timeToGo = arrivalLookup.GetTimeToGo(flight);

        if (flight.HasPassedFeederFix)
        {
            flight.UpdateLandingEstimate(flight.ActualFeederFixTime!.Value.Add(timeToGo));
        }
        else
        {
            var landingEstimate = flight.FeederFixEstimate!.Value.Add(timeToGo);
            flight.UpdateLandingEstimate(landingEstimate);
        }
    }
}
