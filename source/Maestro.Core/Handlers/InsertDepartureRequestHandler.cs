using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class InsertDepartureRequestHandler(
    ISequenceProvider sequenceProvider,
    IPerformanceLookup performanceLookup,
    IArrivalLookup arrivalLookup,
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

        if (flight.EstimatedTimeEnroute is null)
        {
            throw new MaestroException($"{request.Callsign} does not have an ETE.");
        }

        // Calculate ETA based on take-off time
        var landingEstimate = request.TakeOffTime.Add(flight.EstimatedTimeEnroute.Value);
        flight.UpdateLandingEstimate(landingEstimate);

        // Calculate feeder fix estimate
        var feederFixEstimate = GetFeederFixTime(flight);
        if (feederFixEstimate is not null)
            flight.UpdateFeederFixEstimate(feederFixEstimate.Value);

        // Calculate the position in the sequence
        flight.SetState(State.New, clock);

        scheduler.Schedule(lockedSequence.Sequence);

        // Pending flights are made Stable when inserted
        flight.SetState(State.Stable, clock);
    }

    DateTimeOffset? GetFeederFixTime(Flight flight)
    {
        var aircraftPerformance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        if (aircraftPerformance is null)
            return null;

        var arrivalInterval = arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.AssignedArrivalIdentifier,
            flight.AssignedRunwayIdentifier,
            aircraftPerformance);
        if (arrivalInterval is null)
            return null;

        return flight.EstimatedLandingTime.Add(-arrivalInterval.Value);
    }
}
