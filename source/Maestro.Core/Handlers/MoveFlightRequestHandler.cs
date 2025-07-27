using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public record MoveFlightRequest(
    string AirportIdentifier,
    string Callsign,
    string SlotIdentifier,
    string RunwayIdentifier)
    : IRequest;

// TODO: Consider whether the request should provide a specific time, or just the slot.

public class MoveFlightRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator, ILogger logger)
    : IRequestHandler<MoveFlightRequest>
{
    public async Task Handle(MoveFlightRequest request, CancellationToken cancellationToken)
    {
        using var exclusiveSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        var sequence = exclusiveSequence.Sequence;

        var currentSlot = sequence.FindSlotFor(request.Callsign);
        if (currentSlot is null)
            throw new MaestroException($"Flight {request.Callsign} not found in sequence for airport {request.AirportIdentifier}.");

        // Cannot move landed or frozen flights
        var flight = currentSlot.Flight!;
        if (flight.State is State.Frozen or State.Landed)
            throw new MaestroException($"Cannot move a {flight.State} flight.");

        var slots = sequence.Slots.OrderBy(s => s.Time)
            .Where(s => s.RunwayIdentifier == request.RunwayIdentifier)
            .SkipWhile(s => s.Identifier != request.SlotIdentifier)
            .ToArray();

        // Cannot schedule in front of a frozen flight
        var frozenFollowers = slots.Where(s => s.Flight is not null && s.Flight.State == State.Frozen);
        if (frozenFollowers.Any())
            throw new MaestroException("Cannot move a flight in front of a frozen flight.");

        // If a flight is already scheduled in the slot, deallocate it
        var targetSlot = slots.FirstOrDefault();
        if (targetSlot is null)
            throw new MaestroException($"No slot found with ID {request.SlotIdentifier} for runway {request.RunwayIdentifier}.");

        var deallocatedFlight = targetSlot.Flight;
        if (deallocatedFlight is not null)
        {
            if (deallocatedFlight.NoDelay)
                throw new MaestroException("Cannot move a flight to a slot that has a no-delay flight already scheduled.");

            targetSlot.Deallocate();
        }

        // Allocate the desired flight to the slot
        currentSlot.Deallocate();
        targetSlot.AllocateTo(flight);
        if (flight.State == State.Unstable)
            flight.SetState(State.Stable);

        // Move the deallocated flight back one slot
        // Repeat the process until no more flights need to be delayed
        if (deallocatedFlight is not null)
        {
            var currentFlightToMove = deallocatedFlight;

            foreach (var slot in slots.Skip(1))
            {
                if (slot.Flight is null)
                {
                    // Found an empty slot, allocate the current flight and exit
                    slot.AllocateTo(currentFlightToMove);
                    break;
                }

                // Do not move flights that are marked as NoDelay
                if (slot.Flight.NoDelay)
                {
                    continue;
                }

                // This slot is occupied, so we need to move its flight as well
                var flightInSlot = slot.Flight;
                slot.Deallocate();
                slot.AllocateTo(currentFlightToMove);

                // The flight in this slot becomes the next flight to move
                currentFlightToMove = flightInSlot;
            }

            if (currentFlightToMove != null)
            {
                logger.Warning(
                    "{Callsign} was pushed back to the end of the sequence and no slots were available for it.",
                    currentFlightToMove.Callsign);
            }
        }

        // TODO: Publish for all flights that were moved
        await mediator.Publish(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), cancellationToken);
    }
}
