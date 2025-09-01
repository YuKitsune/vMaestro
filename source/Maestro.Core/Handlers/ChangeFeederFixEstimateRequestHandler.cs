using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeFeederFixEstimateRequestHandler(
    ISequenceProvider sequenceProvider,
    IEstimateProvider estimateProvider,
    IScheduler scheduler,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeFeederFixEstimateRequest>
{
    public async Task Handle(ChangeFeederFixEstimateRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return;
        }

        flight.UpdateFeederFixEstimate(request.NewFeederFixEstimate, manual: true);

        // Re-calculate the landing estimate based on the new feeder fix estimate
        var landingEstimate = estimateProvider.GetLandingEstimate(
            flight,
            flight.Fixes.Last().Estimate);
        if (landingEstimate is not null)
            flight.UpdateLandingEstimate(landingEstimate.Value);

        scheduler.Recompute(flight, lockedSequence.Sequence);
        if (flight.State is State.Unstable)
            flight.SetState(State.Stable, clock); // TODO: Make configurable

        await mediator.Publish(
            new SequenceUpdatedNotification(
                lockedSequence.Sequence.AirportIdentifier,
                lockedSequence.Sequence.ToMessage()),
            cancellationToken);
    }
}
