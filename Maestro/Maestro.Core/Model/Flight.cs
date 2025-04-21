using System.Diagnostics;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

[DebuggerDisplay("{Callsign} {ScheduledLandingTime}")]
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

    public State State { get; private set; } = State.Unstable;

    public bool PositionIsFixed => State is not State.Unstable and not State.Stable;

    public DateTimeOffset? InitialFeederFixTime { get; set; }
    public DateTimeOffset? EstimatedFeederFixTime { get; set; } // ETA_FF
    public DateTimeOffset? ScheduledFeederFixTime { get; set; } // STA_FF

    public DateTimeOffset InitialLandingTime { get; set; }
    public DateTimeOffset EstimatedLandingTime { get; set; } // ETA
    public DateTimeOffset ScheduledLandingTime { get; set; } // STA
    public TimeSpan TotalDelay => ScheduledLandingTime - InitialLandingTime;
    public TimeSpan RemainingDelay => ScheduledLandingTime - EstimatedLandingTime;
    
    public bool Activated => ActivatedTime.HasValue;
    public DateTimeOffset? ActivatedTime { get; private set; }
    
    public DateTimeOffset? PositionUpdated { get; private set; }
    public FlightPosition? LastKnownPosition { get; private set; }
    public FixEstimate[] Estimates { get; private set; } = [];
    
    public FlowControls FlowControls { get; private set; } = FlowControls.ProfileSpeed;
    
    public bool HasBeenScheduled { get; private set; }

    public void SetState(State state)
    {
        // TODO: Prevent invalid state changes
        State = state;
    }

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
        if (EstimatedLandingTime > feederFixTime)
            throw new MaestroException($"Cannot schedule {Callsign} to cross feeder fix at {feederFixTime} as it's estimated feeder fix time is {EstimatedFeederFixTime}.");
        
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

    public void SetLandingTime(DateTimeOffset landingTime)
    {
        if (EstimatedLandingTime > landingTime)
            throw new MaestroException($"Cannot schedule {Callsign} to land at {landingTime} as it's estimated landing time is {EstimatedLandingTime}.");
        
        HasBeenScheduled = true;
        ScheduledLandingTime = landingTime;
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

public enum FlowControls
{
    ProfileSpeed,
    S250
}