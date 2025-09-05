using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Scheduling;
using MediatR;

namespace Maestro.Core.Handlers;

public class StartSequencingRequestHandler(
    ISequenceProvider sequenceProvider,
    SchedulerBackgroundService schedulerBackgroundService,
    IMediator mediator)
    : IRequestHandler<StartSequencingRequest>
{
    public async Task Handle(StartSequencingRequest request, CancellationToken cancellationToken)
    {
        await sequenceProvider.InitializeSequence(
            request.AirportIdentifier,
            new RunwayMode
            {
                Identifier = request.RunwayMode.Identifier,
                Runways = request.RunwayMode.Runways.Select(d => new RunwayConfiguration
                {
                    Identifier = d.RunwayIdentifier,
                    LandingRateSeconds = d.AcceptanceRate,

                    // TODO:
                    Dependencies = [],
                    Preferences = null,
                    Requirements = null
                }).ToArray()
            },
            cancellationToken);
        await schedulerBackgroundService.Start(request.AirportIdentifier, cancellationToken);

        var sequenceMessage = sequenceProvider.GetReadOnlySequence(request.AirportIdentifier);
        await mediator.Publish(new SequenceInitializedNotification(request.AirportIdentifier, sequenceMessage), cancellationToken);
    }
}
