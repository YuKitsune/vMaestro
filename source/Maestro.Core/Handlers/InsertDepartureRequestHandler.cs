using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class InsertDepartureRequestHandler(
    ISessionManager sessionManager,
    IPerformanceLookup performanceLookup,
    IArrivalLookup arrivalLookup,
    IClock clock,
    IMediator mediator)
    : IRequestHandler<InsertDepartureRequest>
{
    public async Task Handle(InsertDepartureRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.PendingFlights.SingleOrDefault(f =>
            f.Callsign == request.Callsign);

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

        sequence.Depart(flight, request.TakeOffTime);
        flight.SetState(State.Stable, clock);

        // Calculate feeder fix estimate based on landing time
        // Need to do this after so that the runway gets assigned
        var feederFixEstimate = GetFeederFixTime(flight);
        if (feederFixEstimate is not null)
            flight.UpdateFeederFixEstimate(feederFixEstimate.Value);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
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

        return flight.LandingEstimate.Subtract(arrivalInterval.Value);
    }
}
