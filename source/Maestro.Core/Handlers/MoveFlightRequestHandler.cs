using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class MoveFlightRequestHandler(
    ISessionManager sessionManager,
    IScheduler scheduler,
    IMediator mediator,
    IClock clock)
    : IRequestHandler<MoveFlightRequest>
{
    public async Task Handle(MoveFlightRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.FindTrackedFlight(request.Callsign);
        if (flight is null)
            return;

        var runwayMode = sequence.GetRunwayModeAt(request.NewLandingTime);
        var runwayConfig = runwayMode.Runways
            .FirstOrDefault(r => request.RunwayIdentifiers.Contains(r.Identifier));
        if (runwayConfig is null)
            runwayConfig = runwayMode.Default;

        var optimizedLandingTime = OptimizeLandingTime(
            sequence,
            flight,
            runwayConfig,
            request.NewLandingTime);

        flight.SetLandingTime(optimizedLandingTime, manual: true);
        flight.SetRunway(runwayConfig.Identifier, manual: true);
        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && flight.FeederFixEstimate is not null && !flight.HasPassedFeederFix)
        {
            var totalDelay = optimizedLandingTime - flight.LandingEstimate;
            var feederFixTime = flight.FeederFixEstimate.Value + totalDelay;
            flight.SetFeederFixTime(feederFixTime);
        }

        // Unstable flights become stable when moved
        if (flight.State == State.Unstable)
            flight.SetState(State.Stable, clock);

        scheduler.Schedule(sequence);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }

    DateTimeOffset OptimizeLandingTime(
        Sequence sequence,
        Flight targetFlight,
        Runway runway,
        DateTimeOffset targetLandingTime)
    {
        var proposedLandingTime = targetLandingTime;

        var leader = sequence.Flights
            .FirstOrDefault(f =>
                f != targetFlight &&
                f.AssignedRunwayIdentifier == runway.Identifier &&
                f.LandingTime.IsSameOrBefore(proposedLandingTime));

        var trailer = sequence.Flights
            .FirstOrDefault(f =>
                f != targetFlight &&
                f.AssignedRunwayIdentifier == runway.Identifier &&
                f.LandingTime.IsSameOrAfter(proposedLandingTime));

        if (leader is not null && leader.State == State.Frozen && trailer is not null && trailer.State == State.Frozen)
        {
            var timeBetween = trailer.LandingTime - leader.LandingTime;
            if (timeBetween.TotalSeconds < runway.AcceptanceRate.TotalSeconds * 2)
                throw new MaestroException("Cannot move flight between two frozen flights");
        }

        // Push the landing time back if it's too close to a frozen flight in front of it
        if (leader is not null && leader.State == State.Frozen)
        {
            var timeToLeader = proposedLandingTime - leader.LandingTime;
            if (timeToLeader < runway.AcceptanceRate)
            {
                // Move target flight to be exactly the landing rate after the frozen leader
                proposedLandingTime = leader.LandingTime.Add(runway.AcceptanceRate);
            }
        }

        // Push the flight forward if it's too close to a frozen flight behind it
        if (trailer is not null && trailer.State == State.Frozen)
        {
            var timeToTrailer = trailer.LandingTime - proposedLandingTime;
            if (timeToTrailer < runway.AcceptanceRate)
            {
                // Move target flight to be exactly the landing rate before the frozen trailer
                proposedLandingTime = trailer.LandingTime.Subtract(runway.AcceptanceRate);
            }
        }

        return proposedLandingTime;
    }
}
