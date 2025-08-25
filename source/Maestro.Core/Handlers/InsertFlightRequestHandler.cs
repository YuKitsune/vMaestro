using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class InsertFlightRequestHandler(
    ISequenceProvider sequenceProvider,
    IScheduler scheduler,
    IClock clock,
    IPerformanceLookup performanceLookup,
    IMediator mediator)
    : IRequestHandler<InsertFlightRequest>
{
    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSequence.Sequence;

        var (landingTime, runwayIdentifiers) = FindTargetLandingTime(sequence, request.Options);

        var runwayModeAtLandingTime = sequence.NextRunwayMode is not null
            ? sequence.FirstLandingTimeForNextMode.IsSameOrBefore(landingTime)
                ? sequence.NextRunwayMode
                : sequence.CurrentRunwayMode
            : sequence.CurrentRunwayMode;

        var runwayIdentifier =
            runwayIdentifiers.FirstOrDefault(r => runwayModeAtLandingTime.Runways.Any(rm => rm.Identifier == r)) ??
            runwayModeAtLandingTime.Runways.First().Identifier;

        var state = State.Frozen; // TODO: Make this configurable

        // If the callsign was blank, create a dummy flight
        if (string.IsNullOrWhiteSpace(request.Callsign))
        {
            sequence.AddDummyFlight(landingTime, runwayIdentifier, scheduler, clock);
            await mediator.Publish(
                new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
                cancellationToken);
            return;
        }

        // BUG: If inserting after a frozen flight, nothing happens
        var landedFlight = sequence.Flights.FirstOrDefault(f=> f.Callsign == request.Callsign && f.State == State.Landed);
        if (landedFlight is not null)
        {
            // Re-sequence the landed flight at the specified time
            landedFlight.SetState(State.Overshoot, clock);
            landedFlight.SetRunway(runwayIdentifier, manual: true); // TODO: Just use the estimate
            landedFlight.SetTargetTime(landingTime); // TODO: Just use the estimate

            scheduler.Schedule(sequence);

            landedFlight.SetState(state, clock);

            await mediator.Publish(
                new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
                cancellationToken);
            return;
        }

        var pendingFlight = sequence.Flights.FirstOrDefault(f => f.Callsign == request.Callsign && f.State == State.Pending);
        if (pendingFlight is not null)
        {
            pendingFlight.SetState(State.New, clock);
            pendingFlight.SetRunway(runwayIdentifier, manual: true);
            pendingFlight.SetLandingTime(landingTime);
            scheduler.Schedule(sequence);
            pendingFlight.SetState(State.Stable, clock);
            await mediator.Publish(
                new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
                cancellationToken);
            return;
        }

        // Create a new flight if it does not exist
        if (!string.IsNullOrWhiteSpace(request.Callsign))
        {
            // TODO: Make a default for this somewhere
            var performanceInfo = !string.IsNullOrEmpty(request.AircraftType)
                ? performanceLookup.GetPerformanceDataFor(request.AircraftType)
                : null;

            var flight = new Flight(request.Callsign, request.AirportIdentifier, landingTime)
            {
                AircraftType = request.AircraftType,
                WakeCategory = performanceInfo?.WakeCategory,
            };

            flight.SetRunway(runwayIdentifier, manual: true);
            flight.SetLandingTime(landingTime, manual: true);

            flight.SetState(state, clock);

            sequence.AddFlight(flight, scheduler);
            await mediator.Publish(
                new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
                cancellationToken);
        }
    }

    (DateTimeOffset, string[]) FindTargetLandingTime(Sequence sequence, IInsertFlightOptions options)
    {
        if (options is ExactInsertionOptions exactInsertionOptions)
            return (exactInsertionOptions.TargetLandingTime, exactInsertionOptions.RunwayIdentifiers);

        if (options is not RelativeInsertionOptions relativeInsertionOptions)
            throw new ArgumentOutOfRangeException(nameof(options));

        var referenceFlight = sequence.Flights.SingleOrDefault(f => f.Callsign == relativeInsertionOptions.ReferenceCallsign);
        if (referenceFlight is null)
            throw new MaestroException($"Reference flight {relativeInsertionOptions.ReferenceCallsign} not found");

        if (referenceFlight.State == State.Frozen)
        {
            if (relativeInsertionOptions.Position == RelativePosition.Before)
                throw new MaestroException("Flights cannot be inserted before a frozen flight");

            // Return the reference flights's landing time so the scheduler will push the inserted flight back
            return (referenceFlight.ScheduledLandingTime, [referenceFlight.AssignedRunwayIdentifier]);
        }

        return relativeInsertionOptions.Position switch
        {
            // Return the reference flights landing time so the scheduler will push it back (inserted flights have a higher priority than existing flights)
            RelativePosition.Before => (
                referenceFlight.ScheduledLandingTime,
                [referenceFlight.AssignedRunwayIdentifier]
            ),
            // Add a small buffer to the reference flight to ensure the inserted flight is after it
            RelativePosition.After => (
                referenceFlight.ScheduledLandingTime.AddSeconds(30), // TODO: Configurable to min separation time
                [referenceFlight.AssignedRunwayIdentifier]
            ),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
