using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record MoveFlightRequest(string AirportIdentifier, string Callsign, DateTimeOffset NewLandingTime) : IRequest;

public class MoveFlightRequestHandler : IRequestHandler<MoveFlightRequest>
{
    readonly ISequenceProvider _sequenceProvider;
    readonly IMediator _mediator;

    public MoveFlightRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator)
    {
        _sequenceProvider = sequenceProvider;
        _mediator = mediator;
    }

    public async Task Handle(MoveFlightRequest request, CancellationToken cancellationToken)
    {
        using var exclusiveSequence = await _sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        var sequence = exclusiveSequence.Sequence;

        var flight = sequence.TryGetFlight(request.Callsign);
        if (flight is null)
            return;

        if (flight.State is State.Frozen or State.Landed or State.Desequenced or State.Removed)
            throw new MaestroException($"Cannot move a {flight.State} flight.");


        // Cannot schedule in front of a frozen flight
        var frozenLeaders = sequence.SequencableFlights.Where(f => f.State == State.Frozen && f.ScheduledLandingTime.IsSameOrAfter(request.NewLandingTime));
        if (frozenLeaders.Any())
            throw new MaestroException("Cannot move a flight in front of a frozen flight.");

        flight.SetLandingTime(request.NewLandingTime, manual: true);

        if (flight.State == State.Unstable)
            flight.SetState(State.Stable);

        // --- New logic: Check for flight in front within landing rate ---
        // Find the next flight ahead in sequence (by landing time, same runway)
        var flightsOnRunway = sequence.SequencableFlights
            .Where(f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier && f.Callsign != flight.Callsign)
            .OrderBy(f => f.ScheduledLandingTime)
            .ToList();

        // Determine which runway mode will be in effect at the new landing time
        var runwayMode = sequence.GetRunwayModeAt(request.NewLandingTime);
        var runwayConfig = runwayMode.Runways.FirstOrDefault(r => r.Identifier == flight.AssignedRunwayIdentifier);
        if (runwayConfig != null)
        {
            var landingRate = TimeSpan.FromSeconds(runwayConfig.LandingRateSeconds);
            // Find the first flight scheduled to land after this one
            var nextFlight = flightsOnRunway.FirstOrDefault(f => f.ScheduledLandingTime > request.NewLandingTime);
            if (nextFlight != null && (nextFlight.ScheduledLandingTime - request.NewLandingTime) < landingRate)
            {
                nextFlight.NeedsRecompute = true;
            }

            // --- New logic: Check for trailing flights within landing rate ---
            var trailingFlight = flightsOnRunway.LastOrDefault(f => f.ScheduledLandingTime < request.NewLandingTime);
            if (trailingFlight != null && (request.NewLandingTime - trailingFlight.ScheduledLandingTime) < landingRate)
            {
                trailingFlight.NeedsRecompute = true;
            }
        }

        await _mediator.Publish(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), cancellationToken);
    }
}
