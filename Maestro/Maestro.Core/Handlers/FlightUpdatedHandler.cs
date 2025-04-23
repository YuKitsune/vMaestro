using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    WakeCategory WakeCategory,
    string Origin,
    string Destination,
    string? AssignedRunway,
    string? AssignedArrival,
    bool Activated,
    FlightPosition? Position,
    FixEstimate[] Estimates)
    : INotification;

public class FlightUpdatedHandler(
    ISequenceProvider sequenceProvider,
    IRunwayAssigner runwayAssigner,
    IFlightUpdateRateLimiter rateLimiter,
    IEstimateProvider estimateProvider,
    IScheduler scheduler,
    IMediator mediator,
    IClock clock,
    ILogger<FlightUpdatedHandler> logger)
    : INotificationHandler<FlightUpdatedNotification>
{
    public async Task Handle(FlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug("Received update for {Callsign}", notification.Callsign);
        
        var sequence = sequenceProvider.TryGetSequence(notification.Destination);
        if (sequence is null)
            return;
        
        var flight = await sequence.TryGetFlight(notification.Callsign, cancellationToken);
        if (flight is null)
        {
            // TODO: Make configurable
            var flightCreationThreshold = TimeSpan.FromHours(2);
            
            var feederFix = notification.Estimates.LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
            var landingEstimate = notification.Estimates.Last().Estimate;

            var runway = notification.AssignedRunway is not null
                ? notification.AssignedRunway!
                : FindBestRunway(
                    feederFix?.FixIdentifier ?? string.Empty,
                    notification.AircraftType,
                    sequence.CurrentRunwayMode,
                    sequence.RunwayAssignmentRules);
            
            // TODO: Verify if this behaviour is correct
            // Flights not planned via a feeder fix are added to the pending list
            if (feederFix is null)
            {
                flight = CreateMaestroFlight(
                    notification,
                    runway,
                    null,
                    landingEstimate);
                
                // TODO: Revisit flight plan activation
                flight.Activate(clock);
                
                await sequence.AddPending(flight, cancellationToken);
            }
            // Only create flights in Maestro when they're within a specified range of the feeder fix
            else if (feederFix.Estimate - clock.UtcNow() <= flightCreationThreshold)
            {
                flight = CreateMaestroFlight(
                    notification,
                    runway,
                    feederFix,
                    landingEstimate);
                
                // TODO: Revisit flight plan activation
                flight.Activate(clock);
                
                await sequence.Add(flight, cancellationToken);
            }
        }

        if (flight is null)
            return;

        // Only apply rate limiting if a position is available
        // When no position is available (i.e. Not coupled to a radar track), we accept all updates
        if (notification.Position is not null)
        {
            var shouldUpdate = rateLimiter.ShouldUpdateFlight(flight, notification.Position);
            if (!shouldUpdate)
                return;
        }
        
        flight.UpdateLastSeen(clock);
        
        // TODO: Revisit flight plan activation
        // The flight becomes 'active' in Maestro when the flight is activated in TAAATS.
        // It is then updated by regular reports from the TAAATS FDP to the Maestro System.
        // if (!flight.Activated && notification.Activated)
        // {
        //     flight.Activate(clock);
        // }
        
        // Update flight details
        // flight.AssignedRunwayIdentifier ??= notification.AssignedRunway;
        flight.SetArrival(notification.AssignedArrival);

        if (flight.Activated)
        {
            if (notification.Position is not null)
            {
                await mediator.Publish(
                    new FlightPositionReport(
                        flight.Callsign,
                        flight.DestinationIdentifier,
                        notification.Position,
                        notification.Estimates),
                    cancellationToken);
            }
            
            CalculateEstimates(flight);
            scheduler.Schedule(sequence, flight);
            
            // TODO: Optimise
            
            SetState(flight);
        }
        
        await mediator.Publish(new SequenceModifiedNotification(sequence), cancellationToken);
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

    void SetState(Flight flight)
    {
        // TODO: Make configurable
        var stableThreshold = TimeSpan.FromMinutes(25);
        var frozenThreshold = TimeSpan.FromMinutes(15);
        var minUnstableTime = TimeSpan.FromSeconds(180);

        var timeActive = clock.UtcNow() - flight.ActivatedTime;
        var timeToFeeder = flight.EstimatedFeederFixTime - clock.UtcNow();
        var timeToLanding = flight.EstimatedLandingTime - clock.UtcNow();

        switch (flight.State)
        {
            case State.Unstable when timeToFeeder <= stableThreshold && timeActive > minUnstableTime:
                flight.SetState(State.Stable);
                break;
        
            case State.Stable when flight.InitialFeederFixTime < clock.UtcNow():
                flight.SetState(State.SuperStable);
                break;
        
            case State.SuperStable when timeToLanding <= frozenThreshold:
                flight.SetState(State.Frozen);
                break;
        
            case State.Frozen when flight.ScheduledLandingTime < clock.UtcNow():
                flight.SetState(State.Landed);
                break;
            
            // No change required
            default:
                return;
        }
        
        logger.LogInformation("{Callsign} is now {State}", flight.Callsign, flight.State);
    }

    void CalculateEstimates(Flight flight)
    {
        var feederFixEstimate = estimateProvider.GetFeederFixEstimate(flight);
        if (feederFixEstimate is not null)
        {
            var totalDifference = (feederFixEstimate - flight.EstimatedFeederFixTime!.Value).Value.Duration();
            if (totalDifference.TotalSeconds >= 1)
            {
                logger.LogInformation("{Callsign} ETA FF was {OriginalEstimate} and is now {NewEstimate} ({Difference})",
                    flight.Callsign, flight.EstimatedFeederFixTime, feederFixEstimate,  totalDifference);
            }
                
            flight.UpdateFeederFixEstimate(feederFixEstimate.Value);
        }
            
        var landingEstimate = estimateProvider.GetLandingEstimate(flight);
        if (landingEstimate is not null)
        {
            var totalDifference = (landingEstimate - flight.EstimatedLandingTime).Value.Duration();
            if (totalDifference.TotalSeconds >= 1)
            {
                logger.LogInformation(
                    "{Callsign} ETA was {OriginalEstimate} and is now {NewEstimate} ({Difference})",
                    flight.Callsign, flight.EstimatedFeederFixTime, feederFixEstimate, totalDifference);
            }

            flight.UpdateLandingEstimate(landingEstimate.Value);
        }
    }

    Flight CreateMaestroFlight(
        FlightUpdatedNotification notification,
        string runwayIdentifier,
        FixEstimate? feederFixEstimate,
        DateTimeOffset landingEstimate)
    {
        var flight = new Flight(
            notification.Callsign,
            notification.AircraftType,
            notification.WakeCategory,
            notification.Origin,
            notification.Destination,
            runwayIdentifier,
            feederFixEstimate,
            landingEstimate);

        if (feederFixEstimate is null)
            flight.HighPriority = true;
        
        if (!string.IsNullOrEmpty(notification.AssignedArrival))
            flight.SetArrival(notification.AssignedArrival!);

        return flight;
    }
}