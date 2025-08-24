using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record MoveFlightRequest(
    string AirportIdentifier,
    string Callsign,
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

        // TODO: Don't throw exceptions here, return a result instead.
        if (flight.State is State.Frozen or State.Landed or State.Desequenced or State.Removed)
            throw new MaestroException($"Cannot move a {flight.State} flight.");

        // TODO: Validate that the new landing time will not conflict with other flights that cannot be moved.

        // Cannot schedule in front of a frozen flight
        var frozenLeaders = sequence.Flights.Where(f => f.State == State.Frozen && f.ScheduledLandingTime.IsSameOrAfter(request.NewLandingTime));
        if (frozenLeaders.Any())
            throw new MaestroException("Cannot move a flight in front of a frozen flight.");

        flight.SetLandingTime(request.NewLandingTime, manual: true);

        if (flight.State == State.Unstable)
            flight.SetState(State.Stable, clock);

        scheduler.Schedule(sequence);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
