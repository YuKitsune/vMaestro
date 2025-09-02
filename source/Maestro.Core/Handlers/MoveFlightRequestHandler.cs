using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record MoveFlightRequest(
    string AirportIdentifier,
    string Callsign,
    string[] RunwayIdentifiers,
    DateTimeOffset NewLandingTime)
    : IRequest;

public class MoveFlightRequestHandler(
    ISequenceProvider sequenceProvider,
    IScheduler scheduler,
    IMediator mediator,
    IClock clock)
    : IRequestHandler<MoveFlightRequest>
{
    public async Task Handle(MoveFlightRequest request, CancellationToken cancellationToken)
    {
        using var exclusiveSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        var sequence = exclusiveSequence.Sequence;

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
        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier) && flight.EstimatedFeederFixTime is not null && !flight.HasPassedFeederFix)
        {
            var totalDelay = optimizedLandingTime - flight.EstimatedLandingTime;
            var feederFixTime = flight.EstimatedFeederFixTime.Value + totalDelay;
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
        RunwayConfiguration runwayConfiguration,
        DateTimeOffset targetLandingTime)
    {
        var proposedLandingTime = targetLandingTime;
        var arrivalRate = TimeSpan.FromSeconds(runwayConfiguration.LandingRateSeconds);

        var leader = sequence.Flights
            .FirstOrDefault(f =>
                f != targetFlight &&
                f.AssignedRunwayIdentifier == runwayConfiguration.Identifier &&
                f.ScheduledLandingTime.IsSameOrBefore(proposedLandingTime));

        var trailer = sequence.Flights
            .FirstOrDefault(f =>
                f != targetFlight &&
                f.AssignedRunwayIdentifier == runwayConfiguration.Identifier &&
                f.ScheduledLandingTime.IsSameOrAfter(proposedLandingTime));

        if (leader is not null && leader.State == State.Frozen && trailer is not null && trailer.State == State.Frozen)
        {
            var timeBetween = trailer.ScheduledLandingTime - leader.ScheduledLandingTime;
            if (timeBetween.TotalSeconds < runwayConfiguration.LandingRateSeconds * 2)
                throw new MaestroException("Cannot move flight between two frozen flights");
        }

        // Push the landing time back if it's too close to a frozen flight in front of it
        if (leader is not null && leader.State == State.Frozen)
        {
            var timeToLeader = proposedLandingTime - leader.ScheduledLandingTime;
            if (timeToLeader < arrivalRate)
            {
                // Move target flight to be exactly the landing rate after the frozen leader
                proposedLandingTime = leader.ScheduledLandingTime.Add(arrivalRate);
            }
        }

        // Push the flight forward if it's too close to a frozen flight behind it
        if (trailer is not null && trailer.State == State.Frozen)
        {
            var timeToTrailer = trailer.ScheduledLandingTime - proposedLandingTime;
            if (timeToTrailer < arrivalRate)
            {
                // Move target flight to be exactly the landing rate before the frozen trailer
                proposedLandingTime = trailer.ScheduledLandingTime.Subtract(arrivalRate);
            }
        }

        return proposedLandingTime;
    }
}
