using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeRunwayRequestHandler(ISequenceProvider sequenceProvider, IScheduler scheduler, IClock clock, IMediator mediator, ILogger logger)
    : IRequestHandler<ChangeRunwayRequest, ChangeRunwayResponse>
{
    public async Task<ChangeRunwayResponse> Handle(ChangeRunwayRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new ChangeRunwayResponse();
        }

        var previousState = flight.State;

        flight.SetRunway(request.RunwayIdentifier, true);
        flight.SetState(State.New, clock);

        scheduler.Schedule(lockedSequence.Sequence);

        // Unstable flights become Stable when changing runway
        flight.SetState(previousState == State.Unstable ? State.Stable : previousState, clock);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                lockedSequence.Sequence.AirportIdentifier,
                lockedSequence.Sequence.ToMessage()),
            cancellationToken);

        return new ChangeRunwayResponse();
    }
}
