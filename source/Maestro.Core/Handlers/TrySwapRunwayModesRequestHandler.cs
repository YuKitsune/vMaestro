using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

public class TrySwapRunwayModesRequestHandler(
    ISessionManager sessionManager,
    IMediator mediator)
    : IRequestHandler<TrySwapRunwayModesRequest>
{
    public async Task Handle(TrySwapRunwayModesRequest request, CancellationToken cancellationToken)
    {
        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);

        SessionDto? sessionDto = null;
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;
            var hadPendingSwap = sequence.NextRunwayMode is not null;

            sequence.TrySwapRunwayModes();

            var swapOccurred = hadPendingSwap && sequence.NextRunwayMode is null;
            if (swapOccurred)
                sessionDto = session.Snapshot();
        }

        if (sessionDto is not null)
        {
            await mediator.Publish(
                new SessionUpdatedNotification(
                    session.AirportIdentifier,
                    sessionDto),
                cancellationToken);
        }
    }
}
