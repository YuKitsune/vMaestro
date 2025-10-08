using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

// TODO:
// - [X] Refactor dummy flight into a separate type
// - [X] Separate pending flight insertion from dummy flight insertion
// - [ ] Support InsertPendingRequest in server and WPF
// - [ ] Fix Overshoot insertion
// - [ ] Test

public class InsertFlightRequestHandler(
    ISessionManager sessionManager,
    IClock clock,
    IMediator mediator)
    : IRequestHandler<InsertFlightRequest>, IRequestHandler<InsertPendingRequest>, IRequestHandler<InsertOvershootRequest>
{
    const int MaxCallsignLength = 12; // TODO: Verify the VATSIM limit

    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;

        var callsign = request.Callsign?.ToUpperInvariant().Truncate(MaxCallsignLength) ?? sequence.NewDummyCallsign();
        var state = State.Frozen; // TODO: Make this configurable

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
                sequence.InsertDummyFlight(
                    callsign,
                    request.AircraftType ?? string.Empty,
                    exactInsertionOptions.TargetLandingTime,
                    exactInsertionOptions.RunwayIdentifiers,
                    state);
                break;
            case RelativeInsertionOptions relativeInsertionOptions:
                sequence.InsertDummyFlight(
                    callsign,
                    request.AircraftType ?? string.Empty,
                    relativeInsertionOptions.Position,
                    relativeInsertionOptions.ReferenceCallsign,
                    state);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }

    public async Task Handle(InsertPendingRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;

        var pendingFlight = sequence.PendingFlights.FirstOrDefault(f => f.Callsign == request.Callsign);
        if (pendingFlight is null)
        {
            throw new MaestroException($"{request.Callsign} not found in pending list");
        }

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
