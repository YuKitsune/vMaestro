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
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations().SingleOrDefault(c => c.Identifier == request.AirportIdentifier);
        if (airportConfiguration == null)
        {
            logger.Warning("Airport configuration not found for {AirportIdentifier}.", request.AirportIdentifier);
            return;
        }

        var runwayModeConfig = airportConfiguration.RunwayModes.SingleOrDefault(rm => rm.Identifier == request.RunwayMode.Identifier);
        if (runwayModeConfig is null)
        {
            logger.Warning("Runway mode {RunwayModeIdentifier} not found for {AirportIdentifier}.", request.RunwayMode.Identifier, request.AirportIdentifier);
            return;
        }

        var configuration = new RunwayMode(runwayModeConfig);
        foreach (var kvp in request.RunwayMode.AcceptanceRates)
        {
            var runwayIdentifier = kvp.Key;
            var acceptanceRate = kvp.Value;

            var runway = configuration.Runways.SingleOrDefault(r => r.Identifier == runwayIdentifier);
            if (runway is null)
            {
                logger.Warning("Runway {RunwayIdentifier} not found in mode {RunwayModeIdentifier} for {AirportIdentifier}.", runwayIdentifier, request.RunwayMode.Identifier, request.AirportIdentifier);
            }
            else
            {
                runway.ChangeAcceptanceRate(TimeSpan.FromSeconds(acceptanceRate));
            }
        }

        if (request.FirstLandingTimeForNewMode <= clock.UtcNow())
        {
            sequence.ChangeRunwayMode(configuration);
            logger.Information(
                "Runway changed {AirportIdentifier} to {RunwayModeIdentifier}.",
                request.AirportIdentifier,
                configuration.Identifier);

            await mediator.Publish(
                new CoordinationMessageSentNotification(
                    request.AirportIdentifier,
                    $"Configuration change {request.RunwayMode.Identifier}",
                    new CoordinationDestination.Broadcast()),
                cancellationToken);
        }
        else
        {
            sequence.ChangeRunwayMode(
                configuration,
                request.LastLandingTimeForOldMode,
                request.FirstLandingTimeForNewMode);
            logger.Information(
                "Runway change scheduled for {AirportIdentifier} to {RunwayModeIdentifier} at {RunwayModeChangeTime}.",
                request.AirportIdentifier,
                configuration.Identifier,
                request.FirstLandingTimeForNewMode);

            await mediator.Publish(
                new CoordinationMessageSentNotification(
                    request.AirportIdentifier,
                    $"Configuration change {request.RunwayMode.Identifier} scheduled for {request.LastLandingTimeForOldMode:HH:mm}",
                    new CoordinationDestination.Broadcast()),
                cancellationToken);
        }

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
