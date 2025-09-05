using Maestro.Core.Messages;
using Maestro.Core.Scheduling;
using MediatR;

namespace Maestro.Core.Handlers;

public class StopSequencingRequestHandler(SchedulerBackgroundService schedulerBackgroundService, IMediator mediator)
    : IRequestHandler<StopSequencingRequest>
{
    public async Task Handle(StopSequencingRequest request, CancellationToken cancellationToken)
    {
        await schedulerBackgroundService.Stop(request.AirportIdentifier, cancellationToken);
        await mediator.Publish(new SequenceTerminatedNotification(request.AirportIdentifier), cancellationToken);
    }
}
