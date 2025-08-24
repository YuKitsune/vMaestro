using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

// Test cases:
// - When inserting a flight, it should be sequenced at the target time
// - When inserting a flight, before another one, it should be sequenced at the target time
// - When inserting a flight, after another one, it should be sequenced slightly after the target time
// - When inserting a flight, with a blank callsign, a dummy flight should be created
// - When inserting a flight, that has landed, it is re-sequenced at the specified time
// - When inserting a flight, from the pending list, it is sequenced at the specified time
// - When inserting a flight, that does not exist, it should be created with the specified details
// - When inserting a flight, before a frozen flight, it should throw an exception

public class InsertFlightRequestHandler(ISequenceProvider sequenceProvider, IScheduler scheduler, IClock clock)
    : IRequestHandler<InsertFlightRequest>
{
    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSequence.Sequence;

        // TODO: Find a landing time
        var landingTime = FindTargetLandingTime(sequence, request.Options);

        // If the callsign was blank, create a dummy flight
        if (string.IsNullOrWhiteSpace(request.Callsign))
        {
            sequence.AddDummyFlight(landingTime, request.AircraftType, scheduler, clock);
            return;
        }

        var landedFlight = sequence.Flights.FirstOrDefault(f=> f.Callsign == request.Callsign && f.State == State.Landed);
        if (landedFlight is not null)
        {
            // Re-sequence the landed flight at the specified time
            landedFlight.SetState(State.Overshoot, clock);
            landedFlight.SetTargetTime(landingTime);
            scheduler.Schedule(sequence);
            landedFlight.SetState(State.Frozen, clock); // TODO: Make this state configurable in the future
            return;
        }

        var pendingFlight = sequence.Flights.FirstOrDefault(f => f.Callsign == request.Callsign && f.State == State.Pending);
        if (pendingFlight is not null)
        {
            pendingFlight.SetState(State.New, clock);
            pendingFlight.SetLandingTime(landingTime);
            scheduler.Schedule(sequence);
            pendingFlight.SetState(State.Stable, clock);
            return;
        }

        // Create a new flight if it does not exist
        // TODO: This doesn't seem to work. Flight never gets a scheduled landing time.
        if (!string.IsNullOrWhiteSpace(request.Callsign))
        {
            var flight = new Flight(request.Callsign, request.AirportIdentifier, landingTime)
            {
                AircraftType = request.AircraftType
            };

            sequence.AddFlight(flight, scheduler);
        }
    }

    DateTimeOffset FindTargetLandingTime(Sequence sequence, IInsertFlightOptions options)
    {
        if (options is ExactInsertionOptions exactInsertionOptions)
            return exactInsertionOptions.TargetLandingTime;

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
            return referenceFlight.ScheduledLandingTime;
        }

        return relativeInsertionOptions.Position switch
        {
            // Return the reference flights landing time so the scheduler will push it back (inserted flights have a higher priority than existing flights)
            RelativePosition.Before => referenceFlight.ScheduledLandingTime,
            // Add a small buffer to the reference flight to ensure the inserted flight is after it
            RelativePosition.After => referenceFlight.ScheduledLandingTime
                .AddSeconds(30), // TODO: Configurable to min separation time
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
