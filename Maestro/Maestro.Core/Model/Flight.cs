using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

public class Flight
{
    public required string Callsign { get; init; }
    public required string AircraftType { get; init; }
    public required string OriginIdentifier { get; init; }
    public required string DestinationIdentifier { get; init; }
    public required string? FeederFixIdentifier { get; set; }
    public string? AssignedRunwayIdentifier { get; set; }
    public string? AssignedStarIdentifier { get; set; }
    public required bool HighPriority { get; init; }
    public bool NoDelay { get; set; }

    public State State { get; } = State.Unstable;

    public DateTimeOffset? InitialFeederFixTime { get; set; }
    public DateTimeOffset? EstimatedFeederFixTime { get; set; } // ETA_FF
    public DateTimeOffset? ScheduledFeederFixTime { get; set; } // STA_FF
    public TimeSpan? TotalDelayToFeederFix => InitialFeederFixTime - ScheduledFeederFixTime;
    public TimeSpan? RemainingDelayToFeederFix => EstimatedFeederFixTime - ScheduledFeederFixTime;

    public DateTimeOffset InitialLandingTime { get; set; }
    public DateTimeOffset EstimatedLandingTime { get; set; } // ETA
    public DateTimeOffset ScheduledLandingTime { get; set; } // STA
    public TimeSpan TotalDelayToRunway => InitialLandingTime - ScheduledLandingTime;
    public TimeSpan RemainingDelayToRunway => EstimatedLandingTime - ScheduledLandingTime;
    
    public bool Activated => ActivatedTime.HasValue;
    public DateTimeOffset? ActivatedTime { get; private set; }
    
    public DateTimeOffset? PositionUpdated { get; private set; }
    
    public Position LastKnownPosition { get; private set; }
    public FixEstimate[] Estimates { get; private set; } = [];

    public void SetFeederFix(string feederFixIdentifier, DateTimeOffset feederFixEstimate)
    {
        FeederFixIdentifier = feederFixIdentifier;
        UpdateFeederFixEstimate(feederFixEstimate);
    }

    public void UpdateFeederFixEstimate(DateTimeOffset feederFixEstimate)
    {
        EstimatedFeederFixTime = feederFixEstimate;
        if (State == State.Unstable)
        {
            InitialFeederFixTime = feederFixEstimate;
        }
    }

    public void UpdateLandingEstimate(DateTimeOffset landingEstimate)
    {
        EstimatedLandingTime = landingEstimate;
        if (State == State.Unstable)
        {
            InitialLandingTime = landingEstimate;
        }
    }

    public void Activate(IClock clock)
    {
        if (Activated)
            throw new MaestroException($"{Callsign} is already activated.");
        
        ActivatedTime = clock.UtcNow();
    }

    public void UpdatePosition(Position position, FixEstimate[] estimates, IClock clock)
    {
        LastKnownPosition = position;
        Estimates = estimates;
        PositionUpdated = clock.UtcNow();
    }
}