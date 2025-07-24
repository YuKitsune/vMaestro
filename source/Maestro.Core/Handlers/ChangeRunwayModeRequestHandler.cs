using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

// TODO: Move DTOs.
public class RunwayModeDto(string identifier, RunwayConfigurationDto[] runways)
{
    public string Identifier { get; } = identifier;
    public RunwayConfigurationDto[] Runways { get; } = runways;
}

public class RunwayConfigurationDto(string runwayIdentifier, int landingRateSeconds)
{
    public string RunwayIdentifier { get; } = runwayIdentifier;
    public int AcceptanceRate { get; } = landingRateSeconds;
}

public record ChangeRunwayModeRequest(
    string AirportIdentifier,
    RunwayModeDto RunwayMode,
    DateTimeOffset StartTime)
    : IRequest;

public class ChangeRunwayModeRequestHandler(
    ISequenceProvider sequenceProvider,
    IAirportConfigurationProvider airportConfigurationProvider,
    ISlotBasedScheduler scheduler,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeRunwayModeRequest>
{
    public async Task Handle(ChangeRunwayModeRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations().SingleOrDefault(c => c.Identifier == request.AirportIdentifier);
        if (airportConfiguration == null)
        {
            logger.Warning("Airport configuration not found for {AirportIdentifier}.", request.AirportIdentifier);
            return;
        }

        var configuration = new RunwayMode(request.RunwayMode);

        if (request.StartTime <= clock.UtcNow())
        {
            lockedSequence.Sequence.ChangeRunwayMode(configuration, scheduler, clock);

            logger.Information(
                "Runway changed {AirportIdentifier} to {RunwayModeIdentifier}.",
                request.AirportIdentifier,
                configuration.Identifier);

            await mediator.Publish(
                new RunwayModeChangedNotification(
                    request.AirportIdentifier,
                    request.RunwayMode,
                    null,
                    default),
                cancellationToken);
        }
        else
        {
            lockedSequence.Sequence.ChangeRunwayMode(configuration, scheduler, request.StartTime);

            logger.Information(
                "Runway change scheduled for {AirportIdentifier} to {RunwayModeIdentifier} at {RunwayModeChangeTime}.",
                request.AirportIdentifier,
                configuration.Identifier,
                request.StartTime);

            await mediator.Publish(
                new RunwayModeChangedNotification(
                    request.AirportIdentifier,
                    lockedSequence.Sequence.CurrentRunwayMode.ToMessage(),
                    request.RunwayMode,
                    request.StartTime),
                cancellationToken);
        }
    }
}
