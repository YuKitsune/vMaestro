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
    DateTimeOffset LastLandingTimeForOldMode,
    DateTimeOffset FirstLandingTimeForNewMode)
    : IRequest;

public class ChangeRunwayModeRequestHandler(
    ISequenceProvider sequenceProvider,
    IAirportConfigurationProvider airportConfigurationProvider,
    IScheduler scheduler,
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

        var configuration = new RunwayMode
        {
            Identifier = request.RunwayMode.Identifier,
            Runways = request.RunwayMode.Runways.Select(r =>
                    new RunwayConfiguration
                    {
                        Identifier = r.RunwayIdentifier,
                        LandingRateSeconds = r.AcceptanceRate
                    })
                .ToArray()
        };

        if (request.FirstLandingTimeForNewMode <= clock.UtcNow())
        {
            lockedSequence.Sequence.ChangeRunwayMode(configuration, scheduler);

            logger.Information(
                "Runway changed {AirportIdentifier} to {RunwayModeIdentifier}.",
                request.AirportIdentifier,
                configuration.Identifier);
        }
        else
        {
            lockedSequence.Sequence.ChangeRunwayMode(
                configuration,
                request.LastLandingTimeForOldMode,
                request.FirstLandingTimeForNewMode,
                scheduler);

            logger.Information(
                "Runway change scheduled for {AirportIdentifier} to {RunwayModeIdentifier} at {RunwayModeChangeTime}.",
                request.AirportIdentifier,
                configuration.Identifier,
                request.FirstLandingTimeForNewMode);
        }

        await mediator.Publish(
            new SequenceUpdatedNotification(
                lockedSequence.Sequence.AirportIdentifier,
                lockedSequence.Sequence.ToMessage()),
            cancellationToken);
    }
}
