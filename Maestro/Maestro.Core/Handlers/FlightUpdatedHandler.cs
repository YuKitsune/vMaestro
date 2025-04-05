using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Messages;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    string Origin,
    string Destination,
    string? AssignedRunway,
    string? AssignedArrival,
    Position? Position,
    bool Activated,
    FixDto[] Estimates)
    : INotification;

public class FlightUpdatedHandler(SequenceProvider sequenceProvider, IMediator mediator, IClock clock)
    : INotificationHandler<FlightUpdatedNotification>
{
    public async Task Handle(FlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(notification.Destination);
        if (sequence is null)
            return;
        
        var flight = await sequence.TryGetFlight(notification.Callsign, cancellationToken);

        if (flight is null)
        {
            // TODO: Make configurable
            var flightCreationThreshold = TimeSpan.FromHours(2);
            
            // TODO: Estimates in vatSys don't seem to be actual estimates if the flight isn't moving (preactive?)
            var ffEstimate = notification.Estimates.LastOrDefault(x => sequence.FeederFixes.Contains(x.Identifier));
            var landingEstimate = notification.Estimates.Last();
            
            // Only create flights in Maestro when they're within a specified range
            if (ffEstimate is not null && ffEstimate.Estimate - clock.UtcNow() <= flightCreationThreshold)
            {
                flight = CreateMaestroFlight(
                    notification,
                    ffEstimate,
                    landingEstimate.Estimate);
                await sequence.Add(flight, cancellationToken);
            }

            // TODO: What does maestro do for flights not planned via an FF?
            if (landingEstimate.Estimate - clock.UtcNow() <= flightCreationThreshold)
            {
                flight = CreateMaestroFlight(
                    notification,
                    ffEstimate,
                    landingEstimate.Estimate);
                await sequence.Add(flight, cancellationToken);
            }
        }

        if (flight is null)
            return;

        // The flight becomes 'active' in Maestro when the flight is activated in TAAATS.
        // It is then updated by regular reports from the TAAATS FDP to the Maestro System.
        if (!flight.Activated && notification.Activated)
        {
            flight.Activate(clock);
            if (notification.Position.HasValue)
            {
                flight.UpdatePosition(
                    notification.Position.Value,
                    notification.Estimates.Select(x =>
                            new FixEstimate
                            {
                                FixIdentifier = x.Identifier,
                                Estimate = x.Estimate,
                            })
                        .ToArray(),
                    clock);
            }
        }

        if (flight is { Activated: true, State: State.Unstable })
        {
            // TODO: Calculate ETA_FF
            // TODO: Sequence
            // TODO: Schedule
            // TODO: Calculate STA and STA_FF
            // TODO: Optimise
        }
        
        await mediator.Publish(new SequenceModifiedNotification(sequence.ToDto()), cancellationToken);
    }

    Flight CreateMaestroFlight(
        FlightUpdatedNotification notification,
        FixDto? feederFixEstimate,
        DateTimeOffset landingEstimate)
    {
        return new Flight
        {
            Callsign = notification.Callsign,
            AircraftType = notification.AircraftType,
            OriginIdentifier = notification.Origin,
            DestinationIdentifier = notification.Destination,
            AssignedRunwayIdentifier = notification.AssignedRunway,
            AssignedStarIdentifier = notification.AssignedArrival,
            HighPriority = feederFixEstimate is null,
            
            FeederFixIdentifier = feederFixEstimate?.Identifier,
            InitialFeederFixTime = feederFixEstimate?.Estimate,
            EstimatedFeederFixTime = feederFixEstimate?.Estimate,
            ScheduledFeederFixTime = feederFixEstimate?.Estimate,
            
            InitialLandingTime = landingEstimate,
            EstimatedLandingTime = landingEstimate,
            ScheduledLandingTime = landingEstimate,
        };
    }
}