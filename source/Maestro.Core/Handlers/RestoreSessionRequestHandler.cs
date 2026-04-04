using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RestoreSessionRequestHandler(IMaestroInstanceManager instanceManager, IMediator mediator, ILogger logger)
    : IRequestHandler<RestoreSessionRequest>
{
    public async Task Handle(RestoreSessionRequest request, CancellationToken cancellationToken)
    {
        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            instance.Session.Restore(request.Session);
            sessionDto = instance.Session.Snapshot();
        }

        logger.Debug("Session restored");
        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
