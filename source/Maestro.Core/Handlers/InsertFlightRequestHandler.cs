using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class InsertFlightRequestHandler(
    ISessionManager sessionManager,
    IScheduler scheduler,
    IClock clock,
    IPerformanceLookup performanceLookup,
    IMediator mediator)
    : IRequestHandler<InsertFlightRequest>, IRequestHandler<InsertOvershootRequest>
{
    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Send(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var (landingTime, runwayIdentifiers) = FindTargetLandingTime(sequence, request.Options);
        var runwayModeAtLandingTime = sequence.NextRunwayMode is not null
            ? sequence.FirstLandingTimeForNextMode.IsSameOrBefore(landingTime)
                ? sequence.NextRunwayMode
                : sequence.CurrentRunwayMode
            : sequence.CurrentRunwayMode;

        var runway =
            runwayModeAtLandingTime.Runways.FirstOrDefault(r => runwayIdentifiers.Contains(r.Identifier))?.Identifier ??
            runwayModeAtLandingTime.Default.Identifier;

        var state = State.Frozen; // TODO: Make this configurable

        // If the callsign was blank, create a dummy flight
        if (string.IsNullOrWhiteSpace(request.Callsign))
        {
            sequence.AddDummyFlight(landingTime, runway, scheduler, clock);
            await mediator.Publish(
                new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
                cancellationToken);
            return;
        }

        var pendingFlight = sequence.Flights.FirstOrDefault(f => f.Callsign == request.Callsign && f.State == State.Pending);
        if (pendingFlight is not null)
        {
            pendingFlight.SetState(State.New, clock);
            pendingFlight.SetRunway(runway, manual: true);
            pendingFlight.SetLandingTime(landingTime);
            scheduler.Schedule(sequence);
            pendingFlight.SetState(State.Unstable, clock);
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

            flight.SetRunway(runway, manual: true);
            flight.SetLandingTime(landingTime, manual: true);

            flight.SetState(state, clock);

            sequence.AddFlight(flight, scheduler);
            await mediator.Publish(
                new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
                cancellationToken);
        }
    }

    public async Task Handle(InsertOvershootRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Send(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var (landingTime, runwayIdentifiers) = FindTargetLandingTime(sequence, request.Options);
        var runwayModeAtLandingTime = sequence.NextRunwayMode is not null
            ? sequence.FirstLandingTimeForNextMode.IsSameOrBefore(landingTime)
                ? sequence.NextRunwayMode
                : sequence.CurrentRunwayMode
            : sequence.CurrentRunwayMode;

        var runway =
            runwayModeAtLandingTime.Runways.FirstOrDefault(r => runwayIdentifiers.Contains(r.Identifier))?.Identifier ??
            runwayModeAtLandingTime.Default.Identifier;

        var state = State.Frozen; // TODO: Make this configurable

        // BUG: If inserting after a frozen flight, nothing happens
        var landedFlight = sequence.Flights.FirstOrDefault(f=> f.Callsign == request.Callsign && f.State == State.Landed);
        if (landedFlight is null)
        {
            throw new MaestroException($"Flight {request.Callsign} not found in landed flights");
        }

        // Re-sequence the landed flight at the specified time
        landedFlight.SetState(State.Overshoot, clock);
        landedFlight.SetRunway(runway, manual: true);
        landedFlight.SetLandingTime(landingTime);
        scheduler.Schedule(sequence);
        landedFlight.SetState(state, clock);
        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
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

        if (referenceFlight.State == State.Frozen && relativeInsertionOptions.Position == RelativePosition.Before)
            throw new MaestroException("Flights cannot be inserted before a frozen flight");

        // Calculate time separation based on landing rate
        var referenceTime = referenceFlight.LandingTime;
        var runwayModeAtReferenceTime = GetRunwayModeAtTime(sequence, referenceTime);
        var targetRunway = referenceFlight.AssignedRunwayIdentifier;
        var landingRate = GetTimeSeparationForRunway(runwayModeAtReferenceTime, targetRunway);

        var targetLandingTime = relativeInsertionOptions.Position switch
        {
            RelativePosition.Before => referenceTime.Subtract(landingRate),
            RelativePosition.After => referenceTime.Add(landingRate),
            _ => throw new ArgumentOutOfRangeException()
        };

        return (targetLandingTime, [targetRunway]);
    }

    private static RunwayMode GetRunwayModeAtTime(Sequence sequence, DateTimeOffset time)
    {
        return sequence.NextRunwayMode is not null &&
               sequence.FirstLandingTimeForNextMode.IsSameOrBefore(time)
            ? sequence.NextRunwayMode
            : sequence.CurrentRunwayMode;
    }

    private static TimeSpan GetTimeSeparationForRunway(RunwayMode runwayMode, string runwayIdentifier)
    {
        var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == runwayIdentifier);

        // Default to 60 seconds if runway not found
        // TODO: Log a warning and make this configurable
        return runway?.AcceptanceRate ?? TimeSpan.FromSeconds(60);
    }
}
