using Maestro.Core.Dtos;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

public class Flight
{
    public required string Callsign { get; init; }
    public required string AircraftType { get; init; }
    public required WakeCategory WakeCategory { get; init; }
    public required string OriginIdentifier { get; init; }
    public required string DestinationIdentifier { get; init; }
    public string? FeederFixIdentifier { get; set; }
    public string? AssignedRunwayIdentifier { get; set; }
    public string? AssignedStarIdentifier { get; set; }
    public bool HighPriority { get; set; } = false;
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
    public FlightPosition? LastKnownPosition { get; private set; }
    public FixEstimate[] Estimates { get; private set; } = [];
    
    public PositionPrediction[] Trajectory { get; private set; } = Array.Empty<PositionPrediction>();
    
    public FlowControls FlowControls { get; private set; } = FlowControls.ProfileSpeed;

    public void SetRunway(string runwayIdentifier)
    {
        AssignedRunwayIdentifier = runwayIdentifier;
    }

    public void SetFlowControls(FlowControls flowControls)
    {
        FlowControls = flowControls;
    }

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

    public void SetFeederFixTime(DateTimeOffset feederFixTime)
    {
        ScheduledFeederFixTime = feederFixTime;
    }

    public void UpdateLandingEstimate(DateTimeOffset landingEstimate)
    {
        EstimatedLandingTime = landingEstimate;
        if (State == State.Unstable)
        {
            InitialLandingTime = landingEstimate;
        }
    }

    public void SetLandingTime(DateTimeOffset feederFixTime)
    {
        ScheduledLandingTime = feederFixTime;
    }

    public void Activate(IClock clock)
    {
        if (Activated)
            throw new MaestroException($"{Callsign} is already activated.");
        
        ActivatedTime = clock.UtcNow();
    }

    public void UpdatePosition(FlightPosition position, FixEstimate[] estimates, IClock clock)
    {
        LastKnownPosition = position;
        Estimates = estimates;
        PositionUpdated = clock.UtcNow();
    }
}

public class PositionPrediction
{
    public Coordinate Position { get; }

    public int Altitude
    {
        get;
        
    }
    public TimeSpan Interval { get; }
}

public enum FlowControls
{
    ProfileSpeed,
    MaxSpeed,
    S250
}