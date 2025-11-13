using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RestoreSessionRequestHandler(IMaestroInstanceManager instanceManager, IMediator mediator, ILogger logger)
    : IRequestHandler<RestoreSessionRequest>
{
    public async Task Handle(RestoreSessionRequest request, CancellationToken cancellationToken)
    {
        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            instance.Session.Restore(request.Session);
            sessionMessage = instance.Session.Snapshot();
        }

        logger.Information("Session restored");
        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
