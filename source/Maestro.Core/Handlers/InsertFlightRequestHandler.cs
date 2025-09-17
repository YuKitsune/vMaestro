using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class InsertFlightRequestHandler(
    ISessionManager sessionManager,
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
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;

        var state = State.Frozen; // TODO: Make this configurable

        // If the callsign was blank, create a dummy flight
        if (string.IsNullOrWhiteSpace(request.Callsign))
        {
            switch (request.Options)
            {
                case ExactInsertionOptions exactInsertionOptions:
                    sequence.AddDummyFlight(exactInsertionOptions.TargetLandingTime, exactInsertionOptions.RunwayIdentifiers);
                    break;
                case RelativeInsertionOptions relativeInsertionOptions:
                    sequence.AddDummyFlight(relativeInsertionOptions.Position, relativeInsertionOptions.ReferenceCallsign);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            await mediator.Publish(
                new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
                cancellationToken);
            return;
        }

        var pendingFlight = sequence.PendingFlights.FirstOrDefault(f => f.Callsign == request.Callsign);
        if (pendingFlight is not null)
        {
            switch (request.Options)
            {
                case ExactInsertionOptions exactInsertionOptions:
                    sequence.Insert(pendingFlight, exactInsertionOptions.TargetLandingTime);
                    break;
                case RelativeInsertionOptions relativeInsertionOptions:
                    sequence.Insert(pendingFlight, relativeInsertionOptions.Position, relativeInsertionOptions.ReferenceCallsign);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

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

            var flight = new Flight(request.Callsign, request.AirportIdentifier, DateTimeOffset.MinValue)
            {
                AircraftType = request.AircraftType,
                WakeCategory = performanceInfo?.WakeCategory,
            };

            flight.SetState(state, clock);

            switch (request.Options)
            {
                case ExactInsertionOptions exactInsertionOptions:
                    sequence.Insert(flight, exactInsertionOptions.TargetLandingTime);
                    break;
                case RelativeInsertionOptions relativeInsertionOptions:
                    sequence.Insert(flight, relativeInsertionOptions.Position, relativeInsertionOptions.ReferenceCallsign);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

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
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;

        // BUG: If inserting after a frozen flight, nothing happens
        var landedFlight = sequence.FindTrackedFlight(request.Callsign);
        if (landedFlight is null)
        {
            throw new MaestroException($"Flight {request.Callsign} not found in landed flights");
        }

        sequence.Remove(landedFlight.Callsign);

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
                sequence.Insert(landedFlight, exactInsertionOptions.TargetLandingTime);
                break;
            case RelativeInsertionOptions relativeInsertionOptions:
                sequence.Insert(landedFlight, relativeInsertionOptions.Position, relativeInsertionOptions.ReferenceCallsign);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // TODO: Validate flights cannot be inserted between frozen flights when there is less than 2x the acceptance rate

        landedFlight.SetState(State.Frozen, clock);
        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
