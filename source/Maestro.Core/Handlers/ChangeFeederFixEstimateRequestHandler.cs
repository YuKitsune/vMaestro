using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeFeederFixEstimateRequestHandler(
    ISessionManager sessionManager,
    IEstimateProvider estimateProvider,
    IScheduler scheduler,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeFeederFixEstimateRequest>
{
    public async Task Handle(ChangeFeederFixEstimateRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var flight = sequence.FindTrackedFlight(request.Callsign);
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

        scheduler.Recompute(flight, sequence);
        if (flight.State is State.Unstable)
            flight.SetState(State.Stable, clock); // TODO: Make configurable

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
