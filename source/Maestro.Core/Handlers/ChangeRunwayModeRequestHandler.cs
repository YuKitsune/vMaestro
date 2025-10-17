using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeRunwayModeRequestHandler(
    ISessionManager sessionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeRunwayModeRequest>
{
    public async Task Handle(ChangeRunwayModeRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;

        var newRunwayMode = new RunwayMode(request.RunwayMode);

        if (request.FirstLandingTimeForNewMode <= clock.UtcNow())
        {
            sequence.ChangeRunwayMode(newRunwayMode);
            logger.Information(
                "Runway changed {AirportIdentifier} to {RunwayModeIdentifier}.",
                request.AirportIdentifier,
                newRunwayMode.Identifier);

            await mediator.Publish(new InformationNotification(request.AirportIdentifier, clock.UtcNow(), $"Configuration change {request.RunwayMode.Identifier}"), cancellationToken);
        }
        else
        {
            sequence.ChangeRunwayMode(
                newRunwayMode,
                request.LastLandingTimeForOldMode,
                request.FirstLandingTimeForNewMode);
            logger.Information(
                "Runway change scheduled for {AirportIdentifier} to {RunwayModeIdentifier} at {RunwayModeChangeTime}.",
                request.AirportIdentifier,
                newRunwayMode.Identifier,
                request.FirstLandingTimeForNewMode);

            await mediator.Publish(new InformationNotification(request.AirportIdentifier, clock.UtcNow(), $"Configuration change {request.RunwayMode.Identifier} scheduled for {request.LastLandingTimeForOldMode:HH:mm}"), cancellationToken);
        }

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
