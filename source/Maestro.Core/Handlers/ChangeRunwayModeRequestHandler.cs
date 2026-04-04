using Maestro.Contracts.Coordination;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeRunwayModeRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeRunwayModeRequest>
{
    public async Task Handle(ChangeRunwayModeRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ChangeRunwayModeRequest for {AirportIdentifier} to {RunwayMode}", request.AirportIdentifier, request.RunwayMode.Identifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Changing runway mode for {AirportIdentifier} to {RunwayMode}", request.AirportIdentifier, request.RunwayMode.Identifier);

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

            var runwayModeConfig = airportConfiguration.RunwayModes.SingleOrDefault(rm => rm.Identifier == request.RunwayMode.Identifier);
            if (runwayModeConfig is null)
            {
                logger.Warning("Runway mode {RunwayModeIdentifier} not found for {AirportIdentifier}.", request.RunwayMode.Identifier, request.AirportIdentifier);
                return;
            }

            var configuration = new RunwayMode(runwayModeConfig);
            foreach (var runwayDto in request.RunwayMode.Runways)
            {
                var runwayIdentifier = runwayDto.Identifier;
                var acceptanceRate = runwayDto.AcceptanceRateSeconds;

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

                await mediator.Send(
                    new SendCoordinationMessageRequest(
                        request.AirportIdentifier,
                        clock.UtcNow(),
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

                await mediator.Send(
                    new SendCoordinationMessageRequest(
                        request.AirportIdentifier,
                        clock.UtcNow(),
                        $"Configuration change {request.RunwayMode.Identifier} scheduled for {request.LastLandingTimeForOldMode:HH:mm}",
                        new CoordinationDestination.Broadcast()),
                    cancellationToken);
            }

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
