using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public class RecomputeRequestHandler(
    ISequenceProvider sequenceProvider,
    IRunwayAssigner runwayAssigner,
    IEstimateProvider estimateProvider,
    IScheduler scheduler,
    IMediator mediator,
    ILogger<RecomputeRequestHandler> logger)
    : IRequestHandler<RecomputeRequest, RecomputeResponse>
{
    public async Task<RecomputeResponse> Handle(RecomputeRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.LogWarning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new RecomputeResponse();
        }
        
        var flight = await sequence.TryGetFlight(request.Callsign, cancellationToken);
        if (flight == null)
        {
            logger.LogWarning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RecomputeResponse();
        }
        
        logger.LogInformation("Recomputing {Callsign}", request.Callsign);
        
        // Compute estimates
        var feederFix = flight.Estimates.LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
        if (feederFix is not null)
            flight.SetFeederFix(feederFix.FixIdentifier, feederFix.Estimate);
        
        flight.HighPriority = feederFix is null;
        flight.NoDelay = false;
        
        // Re-assign runway if it has not been manually assigned
        if (!flight.RunwayManuallyAssigned)
        {
            var runway = FindBestRunway(
                feederFix?.FixIdentifier ?? string.Empty,
                flight.AircraftType,
                sequence.CurrentRunwayMode,
                sequence.RunwayAssignmentRules);
            
            flight.SetRunway(runway, false);
        }

        var feederFixEstimate = estimateProvider.GetFeederFixEstimate(flight);
        if (feederFixEstimate is not null)
        {
            flight.UpdateFeederFixEstimate(feederFixEstimate.Value);
        }

        var landingEstimate = estimateProvider.GetLandingEstimate(flight);
        if (landingEstimate is not null)
        {
            flight.UpdateLandingEstimate(landingEstimate.Value);
        }
        
        // Reset scheduled times to estimated times
        if (flight.ScheduledFeederFixTime is not null && flight.EstimatedFeederFixTime is not null)
            flight.SetFeederFixTime(flight.EstimatedFeederFixTime.Value);
        
        flight.SetLandingTime(flight.EstimatedLandingTime);
        
        // Reposition in sequence based on new estimates
        await sequence.Sort(cancellationToken);
        
        // Schedule using the new times
        scheduler.Schedule(sequence, flight);
        
        // TODO: Optimise
        
        await mediator.Publish(new MaestroFlightUpdatedNotification(flight), cancellationToken);
        return new RecomputeResponse();
    }

    string FindBestRunway(string feederFixIdentifier, string aircraftType, RunwayModeConfiguration runwayMode, IReadOnlyCollection<RunwayAssignmentRule> assignmentRules)
    {
        var defaultRunway = runwayMode.Runways.First().Identifier;
        if (string.IsNullOrEmpty(feederFixIdentifier))
            return defaultRunway;
        
        var possibleRunways = runwayAssigner.FindBestRunways(
            aircraftType,
            feederFixIdentifier,
            assignmentRules);

        var runwaysInMode = possibleRunways
            .Where(r => runwayMode.Runways.Any(r2 => r2.Identifier == r))
            .ToArray();
        
        // No runways found, use the default one
        if (!runwaysInMode.Any())
            return defaultRunway;

        // TODO: Use lower priorities depending on traffic load.
        //  How could we go about this? Probe for shortest delay? Round-robin?
        var topPriority = runwaysInMode.First();
        return topPriority;
    }
}