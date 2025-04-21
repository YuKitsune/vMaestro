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
    IEstimateProvider estimateProvider,
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
        var flightWasCreated = false;
        if (flight is null)
        {
            // TODO: Make configurable
            var flightCreationThreshold = TimeSpan.FromHours(2);
            
            var feederFix = notification.Estimates.LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
            var landingEstimate = notification.Estimates.Last().Estimate;
            
            // TODO: Verify if this behaviour is correct
            // Flights not planned via a feeder fix are added to the pending list
            if (feederFix is null)
            {
                flight = CreateMaestroFlight(notification, null, landingEstimate);
                
                // TODO: Revisit flight plan activation
                flight.Activate(clock);
                
                await sequence.AddPending(flight, cancellationToken);
                flightWasCreated = true;
            }
            // Only create flights in Maestro when they're within a specified range of the feeder fix
            else if (feederFix.Estimate - clock.UtcNow() <= flightCreationThreshold)
            {
                flight = CreateMaestroFlight(
                    notification,
                    feederFix,
                    landingEstimate);
                
                // TODO: Revisit flight plan activation
                flight.Activate(clock);
                
                await sequence.Add(flight, cancellationToken);
                flightWasCreated = true;
            }
        }

        if (flight is null)
        {
            return;
        }
        
        // TODO: Revisit flight plan activation
        // The flight becomes 'active' in Maestro when the flight is activated in TAAATS.
        // It is then updated by regular reports from the TAAATS FDP to the Maestro System.
        // if (!flight.Activated && notification.Activated)
        // {
        //     flight.Activate(clock);
        // }
        
        // Update flight details
        flight.AssignedRunwayIdentifier ??= notification.AssignedRunway;
        flight.AssignedStarIdentifier = notification.AssignedArrival;

        if (flight is { Activated: true })
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
            
            SetState(flight);
            
            // TODO: Should this be limited to unstable?
            
            // Calculate estimates
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
            
            // Reposition in sequence if necessary
            if (!flightWasCreated && !flight.PositionIsFixed)
                await sequence.RepositionByEstimate(flight, cancellationToken);
            
            // TODO: Schedule
            
            // TODO: Calculate STA and STA_FF
            
            // TODO: Optimise
        }
        
        await mediator.Publish(new SequenceModifiedNotification(sequence), cancellationToken);
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

    Flight CreateMaestroFlight(
        FlightUpdatedNotification notification,
        FixEstimate? feederFixEstimate,
        DateTimeOffset landingEstimate)
    {
        return new Flight
        {
            Callsign = notification.Callsign,
            AircraftType = notification.AircraftType,
            WakeCategory = notification.WakeCategory,
            OriginIdentifier = notification.Origin,
            DestinationIdentifier = notification.Destination,
            AssignedRunwayIdentifier = notification.AssignedRunway,
            AssignedStarIdentifier = notification.AssignedArrival,
            HighPriority = feederFixEstimate is null,
            
            FeederFixIdentifier = feederFixEstimate?.FixIdentifier,
            InitialFeederFixTime = feederFixEstimate?.Estimate,
            EstimatedFeederFixTime = feederFixEstimate?.Estimate,
            ScheduledFeederFixTime = feederFixEstimate?.Estimate,
            
            InitialLandingTime = landingEstimate,
            EstimatedLandingTime = landingEstimate,
            ScheduledLandingTime = landingEstimate
        };
    }
}