using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    WakeCategory WakeCategory,
    string Origin,
    string Destination,
    string? AssignedArrival,
    string? AssignedRunway,
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
    ILogger logger)
    : INotificationHandler<FlightUpdatedNotification>
{
    public async Task Handle(FlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            if (!sequenceProvider.CanSequenceFor(notification.Destination))
                return;

            logger.Verbose("Received update for {Callsign}", notification.Callsign);

            using var lockedSequence = await sequenceProvider.GetSequence(notification.Destination, cancellationToken);
            var sequence = lockedSequence.Sequence;

            bool isNew = false;
            var flight = sequence.TryGetFlight(notification.Callsign);
            if (flight is null)
            {
                isNew = true;
                
                // TODO: Make configurable
                var flightCreationThreshold = TimeSpan.FromHours(2);

                // Use system estimates initially. We will refine them later.
                var feederFix =
                    notification.Estimates.LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
                var landingEstimate = notification.Estimates.Last().Estimate;

                // Prefer the runway assigned in vatSys
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

                    sequence.AddPending(flight);
                    logger.Information("{Callsign} created (pending)", notification.Callsign);
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

                    sequence.Add(flight);
                    logger.Information("{Callsign} created", notification.Callsign);
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
                {
                    logger.Verbose("Rate limiting {Callsign}", notification.Callsign);
                    return;
                }
            }
            
            logger.Debug("Updating {Callsign}", notification.Callsign);

            flight.UpdateLastSeen(clock);
            flight.SetArrival(notification.AssignedArrival);

            // TODO: Revisit flight plan activation
            // The flight becomes 'active' in Maestro when the flight is activated in TAAATS.
            // It is then updated by regular reports from the TAAATS FDP to the Maestro System.
            // if (!flight.Activated && notification.Activated)
            // {
            //     flight.Activate(clock);
            // }

            // Exit early if the flight should not be processed
            if (!flight.Activated ||
                flight.State == State.Desequenced ||
                flight.State == State.Removed ||
                flight.State == State.Landed)
            {
                if (!flight.Activated)
                    logger.Debug("{Callsign} is not activated. No additional processing required.",
                        notification.Callsign);
                else
                    logger.Debug("{Callsign} is {State}. No additional processing required.", notification.Callsign,
                        flight.State);

                return;
            }

            if (flight.NeedsRecompute)
            {
                logger.Information("Recomputing {Callsign}", flight.Callsign);

                // Reset the feeder fix in case of a reroute
                var feederFix =
                    notification.Estimates.LastOrDefault(x => sequence.FeederFixes.Contains(x.FixIdentifier));
                if (feederFix is not null)
                    flight.SetFeederFix(feederFix.FixIdentifier, feederFix.Estimate, feederFix.ActualTimeOver);

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

                flight.NeedsRecompute = false;
            }

            // Compute ETA and ETA_FF
            CalculateEstimates(flight, notification);

            // TODO: Optimise runway selection

            // Schedule the flight if we just added it
            if (isNew)
            {
                scheduler.Schedule(sequence, flight);
            }

            SetState(flight);

            await mediator.Publish(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), cancellationToken);
            logger.Debug("Flight updated: {Flight}", flight);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Error updating {Callsign}", notification.Callsign);
        }
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

        // Keep the flight unstable until it's passed the minimum unstable time
        if (timeActive < minUnstableTime)
        {
            flight.SetState(State.Unstable);
            return;
        }
        
        if (flight.ScheduledLandingTime <= clock.UtcNow())
        {
            flight.SetState(State.Landed);
        }
        else if (timeToLanding <= frozenThreshold)
        {
            flight.SetState(State.Frozen);
        }
        else if (flight.InitialFeederFixTime <= clock.UtcNow())
        {
            flight.SetState(State.SuperStable);
        }
        else if (timeToFeeder <= stableThreshold)
        {
            flight.SetState(State.Stable);
        }
        else
        {
            // No change required
            return;
        }
        
        logger.Information("{Callsign} is now {State}", flight.Callsign, flight.State);
    }

    void CalculateEstimates(Flight flight, FlightUpdatedNotification notification)
    {
        var feederFixSystemEstimate = notification.Estimates.LastOrDefault(e => e.FixIdentifier == flight.FeederFixIdentifier);
        if (!flight.HasPassedFeederFix && feederFixSystemEstimate?.ActualTimeOver is not null)
        {
            flight.PassedFeederFix(feederFixSystemEstimate.ActualTimeOver.Value);
            logger.Information(
                "{Callsign} passed {FeederFix} at {ActualTimeOver}",
                flight.Callsign,
                flight.FeederFixIdentifier,
                feederFixSystemEstimate.ActualTimeOver);
        }

        // Don't update ETA_FF once passed FF
        if (feederFixSystemEstimate is not null && !flight.HasPassedFeederFix)
        {
            var calculatedFeederFixEstimate = estimateProvider.GetFeederFixEstimate(
                flight.FeederFixIdentifier!,
                feederFixSystemEstimate!.Estimate,
                notification.Position);
            if (calculatedFeederFixEstimate is not null && flight.EstimatedFeederFixTime is not null)
            {
                var diff = flight.EstimatedFeederFixTime.Value - calculatedFeederFixEstimate.Value;
                flight.UpdateFeederFixEstimate(calculatedFeederFixEstimate.Value);
                logger.Debug(
                    "{Callsign} ETA_FF now {FeederFixEstimate} (diff {Difference})",
                    flight.Callsign,
                    flight.EstimatedFeederFixTime,
                    diff.ToHoursAndMinutesString());
                
                if (diff.Duration() > TimeSpan.FromMinutes(2)) 
                    logger.Warning("{Callsign} ETA_FF has changed by more than 2 minutes", flight.Callsign);
            }
        }

        var landingSystemEstimate = notification.Estimates.LastOrDefault();
        var calculatedLandingEstimate = estimateProvider.GetLandingEstimate(flight, landingSystemEstimate?.Estimate);
        if (calculatedLandingEstimate is not null)
        {
            var diff = flight.EstimatedLandingTime - calculatedLandingEstimate.Value;
            flight.UpdateLandingEstimate(calculatedLandingEstimate.Value);
            logger.Debug(
                "{Callsign} ETA now {LandingEstimate} (diff {Difference})",
                flight.Callsign,
                flight.EstimatedLandingTime,
                diff.ToHoursAndMinutesString());
                
            if (diff.Duration() > TimeSpan.FromMinutes(2)) 
                logger.Warning("{Callsign} ETA has changed by more than 2 minutes", flight.Callsign);
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
        
        return flight;
    }
}