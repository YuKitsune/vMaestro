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
    DateTimeOffset StartTime,
    bool ReAssignRunways)
    : IRequest;

public class ChangeRunwayModeRequestHandler(
    ISequenceProvider sequenceProvider,
    IAirportConfigurationProvider airportConfigurationProvider,
    IRunwayAssigner runwayAssigner,
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

        DateTimeOffset startTime;
        if (request.StartTime <= clock.UtcNow())
        {
            startTime = clock.UtcNow();
            lockedSequence.Sequence.ChangeRunwayMode(configuration);

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
            startTime = request.StartTime;
            lockedSequence.Sequence.ChangeRunwayMode(configuration, request.StartTime);

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
                    startTime),
                cancellationToken);
        }

        if (request.ReAssignRunways)
        {
            logger.Information(
                "Reassigning runways for {AirportIdentifier} arrivals arriving after {RunwayModeChangeTime}",
                request.AirportIdentifier,
                startTime);
            
            // TODO: Duplicate of FlightUpdatedHandler.FindBestRunway(). Consolidate.
            
            var arrivalsToReassign =
                lockedSequence.Sequence.Flights.Where(f => f.EstimatedLandingTime >= startTime);
            
            foreach (var arrival in arrivalsToReassign)
            {
                // TODO: Override manual assignment?
                var defaultRunway = configuration.Runways.First();
                if (string.IsNullOrEmpty(arrival.FeederFixIdentifier))
                {
                    arrival.SetRunway(defaultRunway.Identifier, manual: false);
                    continue;
                }
                
                var possibleRunways = runwayAssigner.FindBestRunways(
                    arrival.AircraftType,
                    arrival.FeederFixIdentifier,
                    lockedSequence.Sequence.RunwayAssignmentRules);
                
                var runwaysInMode = possibleRunways
                    .Where(r => configuration.Runways.Any(r2 => r2.Identifier == r))
                    .ToArray();
                
                var runwayToAssign = runwaysInMode.Any()
                    ? runwaysInMode.First()
                    : configuration.Runways.First().Identifier;
                
                arrival.SetRunway(runwayToAssign, manual: false);
            }
        }
    }
}