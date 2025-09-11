using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ScheduleRequestHandler(
    ISessionManager sessionManager,
    IScheduler scheduler,
    SequenceCleaner sequenceCleaner,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : IRequestHandler<ScheduleRequest>
{
    public async Task Handle(ScheduleRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSession.Session.Sequence;

        sequence.TrySwapRunwayModes(clock);

        logger.Information("Scheduling {AirportIdentifier}", request.AirportIdentifier);
        scheduler.Schedule(sequence);
        logger.Debug("Completed scheduling {AirportIdentifier}", request.AirportIdentifier);

        logger.Debug("Updating flight states for {AirportIdentifier}", request.AirportIdentifier);
        foreach (var flight in sequence.Flights)
        {
            flight.UpdateStateBasedOnTime(clock);
        }
        logger.Debug("Completed updating flight states for {AirportIdentifier}", request.AirportIdentifier);

        logger.Debug("Cleaning {AirportIdentifier}", request.AirportIdentifier);
        sequenceCleaner.CleanUpFlights(sequence);
        logger.Debug("Completed cleaning {AirportIdentifier}", request.AirportIdentifier);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
