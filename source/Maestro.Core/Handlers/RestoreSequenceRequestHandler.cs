using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RestoreSequenceRequestHandler(ISessionManager sessionManager, IMediator mediator, ILogger logger)
    : IRequestHandler<RestoreSequenceRequest>
{
    public async Task Handle(RestoreSequenceRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence =  await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        lockedSequence.Session.Sequence.Restore(request.Sequence);

        logger.Information("Sequence restored");

        await mediator.Publish(new SequenceUpdatedNotification(request.AirportIdentifier, lockedSequence.Session.Sequence.ToMessage()), cancellationToken);
    }
}
