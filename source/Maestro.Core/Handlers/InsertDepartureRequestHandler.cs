using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

// Test cases:
// - When inserting a flight, the landing estimate should be calculated based on the take-off time and flight time
// - When inserting a flight, the feeder-fix estimate should be calculated based on the take-off time and flight time
// - When inserting a flight, and it's estimated landing time conflicts with existing flights, it should be delayed
// - When inserting a flight, and the take-off time is in the past, it should throw an exception

public class InsertDepartureRequestHandler(
    ISequenceProvider sequenceProvider,
    IScheduler scheduler,
    IClock clock)
    : IRequestHandler<InsertDepartureRequest>
{
    public async Task Handle(InsertDepartureRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.Flights.SingleOrDefault(f =>
            f.Callsign == request.Callsign &&
            f.State == State.Pending);

        if (flight is null)
        {
            // TODO: Confirm what should happen in this case
            // The UI seems to accept manual input
            // Maybe use Aircraft type to determine a speed and figure out a landing time from there?
            throw new MaestroException($"{request.Callsign} was not found in the pending list.");
        }

        // Calculate ETA based on take-off time
        var landingEstimate = request.TakeOffTime.Add(flight.EstimatedFlightTime);
        flight.UpdateLandingEstimate(landingEstimate);

        // TODO: Calculate feeder fix estimate

        // Calculate the position in the sequence
        flight.SetState(State.New, clock);

        scheduler.Schedule(lockedSequence.Sequence);

        // Pending flights are made Stable when inserted
        flight.SetState(State.Stable, clock);
    }
}
