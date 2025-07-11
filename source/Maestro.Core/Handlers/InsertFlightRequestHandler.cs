using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class InsertFlightOvershootRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator)
    : IRequestHandler<InsertOvershootFlightRequest>
{
    public async Task Handle(InsertOvershootFlightRequest request, CancellationToken cancellationToken)
    {
        using var exclusiveSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        var sequence = exclusiveSequence.Sequence;

        var flight = sequence.TryGetFlight(request.Callsign);
        if (flight is null)
            return;

        if (flight.State != State.Landed)
            throw new MaestroException("Cannot insert an overshoot if the flight hasn't landed.");

        var referenceFlight = sequence.TryGetFlight(request.ReferenceCallsign);
        if (referenceFlight is null)
            throw new MaestroException($"Reference flight {request.ReferenceCallsign} not found.");

        var runwayMode = sequence.GetRunwayModeAt(referenceFlight.ScheduledLandingTime);
        var runwayConfig = runwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlight.AssignedRunwayIdentifier);
        if (runwayConfig is null)
            throw new MaestroException($"Runway configuration for {referenceFlight.AssignedRunwayIdentifier} not found.");

        var landingInterval = request.InsertionPoint == InsertionPoint.Before
            ? TimeSpan.FromSeconds(runwayConfig.LandingRateSeconds).Negate()
            : TimeSpan.FromSeconds(runwayConfig.LandingRateSeconds);

        var newLandingTime = referenceFlight.ScheduledLandingTime.Add(landingInterval);

        flight.SetState(State.Frozen); // What should the state be here?
        flight.SetRunway(runwayConfig.Identifier, manual: true);
        flight.SetLandingTime(newLandingTime);

        // TODO: Force anyone behind this flight to recompute

        await mediator.Publish(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), cancellationToken);
    }
}

public class InsertPendingFlightOvershootRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator)
    : IRequestHandler<InsertPendingFlightRequest>
{
    public async Task Handle(InsertPendingFlightRequest request, CancellationToken cancellationToken)
    {
        using var exclusiveSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);
        var sequence = exclusiveSequence.Sequence;

        var flight = sequence.TryGetFlight(request.Callsign);
        if (flight is null)
            return;

        var referenceFlight = sequence.TryGetFlight(request.ReferenceCallsign);
        if (referenceFlight is null)
            throw new MaestroException($"Reference flight {request.ReferenceCallsign} not found.");

        var runwayMode = sequence.GetRunwayModeAt(referenceFlight.ScheduledLandingTime);
        var runwayConfig = runwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlight.AssignedRunwayIdentifier);
        if (runwayConfig is null)
            throw new MaestroException($"Runway configuration for {referenceFlight.AssignedRunwayIdentifier} not found.");

        var landingInterval = request.InsertionPoint == InsertionPoint.Before
            ? TimeSpan.FromSeconds(runwayConfig.LandingRateSeconds).Negate()
            : TimeSpan.FromSeconds(runwayConfig.LandingRateSeconds);

        var newLandingTime = referenceFlight.ScheduledLandingTime.Add(landingInterval);

        flight.SetState(State.Stable); // What should the state be here?
        flight.SetRunway(runwayConfig.Identifier, manual: true);
        flight.SetLandingTime(newLandingTime);

        // TODO: Force anyone behind this flight to recompute

        await mediator.Publish(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), cancellationToken);
    }
}
