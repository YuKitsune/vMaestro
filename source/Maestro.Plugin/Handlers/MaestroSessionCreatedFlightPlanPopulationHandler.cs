using Maestro.Core.Sessions.Contracts;
using MediatR;
using vatsys;

namespace Maestro.Plugin.Handlers;

public class MaestroSessionCreatedFlightPlanPopulationHandler(IMediator mediator)
    : INotificationHandler<MaestroSessionCreatedNotification>
{
    public async Task Handle(MaestroSessionCreatedNotification notification, CancellationToken cancellationToken)
    {
        // When the session starts, we need to ensure Maestro is populated with FDRs, as it may have missed FDR updates
        // that happened before the session started.
        foreach (var fdr in FDP2.GetFDRs)
        {
            await mediator.Send(new TransmitFdrRequest(fdr), cancellationToken);
        }
    }
}
