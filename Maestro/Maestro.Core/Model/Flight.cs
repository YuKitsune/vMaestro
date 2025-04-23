using System.Diagnostics;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

[DebuggerDisplay("{Callsign} {ScheduledLandingTime}")]
public class Flight
{
    public Flight(
        string callsign,
        string aircraftType,
        WakeCategory wakeCategory,
        string originIdentifier,
        string destinationIdentifier,
        string assignedRunwayIdentifier,
        FixEstimate? feederFixEstimate,
        DateTimeOffset initialLandingTime)
    {
        Callsign = callsign;
        AircraftType = aircraftType;
        WakeCategory = wakeCategory;
        OriginIdentifier = originIdentifier;
        DestinationIdentifier = destinationIdentifier;
        AssignedRunwayIdentifier = assignedRunwayIdentifier;

        FeederFixIdentifier = feederFixEstimate?.FixIdentifier;
        InitialFeederFixTime = feederFixEstimate?.Estimate;
        EstimatedFeederFixTime = feederFixEstimate?.Estimate;
        ScheduledFeederFixTime = feederFixEstimate?.Estimate;
        
        InitialLandingTime = initialLandingTime;
        EstimatedLandingTime = initialLandingTime;
        ScheduledLandingTime = initialLandingTime;
    }

    public string Callsign { get; private set; }
    public string AircraftType { get; private set; }
    public WakeCategory WakeCategory { get; private set; }
    public string OriginIdentifier { get; private set; }
    public string DestinationIdentifier { get; private set; }
    public string AssignedRunwayIdentifier { get; private set; }
    public string? AssignedStarIdentifier { get; private set; }
    public bool HighPriority { get; set; } = false;
    public bool NoDelay { get; set; }

    public State State { get; private set; } = State.Unstable;
    public bool PositionIsFixed => State is not State.Unstable and not State.Stable;

    public string? FeederFixIdentifier { get; private set; }
    public DateTimeOffset? InitialFeederFixTime { get; private set; }
    public DateTimeOffset? EstimatedFeederFixTime { get; private set; } // ETA_FF
    public DateTimeOffset? ScheduledFeederFixTime { get; private set; } // STA_FF

    public DateTimeOffset InitialLandingTime { get; private set; }
    public DateTimeOffset EstimatedLandingTime { get; private set; } // ETA
    public DateTimeOffset ScheduledLandingTime { get; private set; } // STA
    public TimeSpan TotalDelay => ScheduledLandingTime - InitialLandingTime;
    public TimeSpan RemainingDelay => ScheduledLandingTime - EstimatedLandingTime;
    
    public bool Activated => ActivatedTime.HasValue;
    public DateTimeOffset? ActivatedTime { get; private set; }
    
    public FlightPosition? LastKnownPosition { get; private set; }
    public FixEstimate[] Estimates { get; private set; } = [];
    
    public FlowControls FlowControls { get; private set; } = FlowControls.ProfileSpeed;
    
    public bool HasBeenScheduled { get; private set; }
    
    public DateTimeOffset LastSeen { get; private set; }

    public void SetState(State state)
    {
        // TODO: Prevent invalid state changes
        State = state;
    }

    public void SetRunway(string runwayIdentifier)
    {
        AssignedRunwayIdentifier = runwayIdentifier;
    }

    public void SetArrival(string arrivalIdentifier)
    {
        AssignedStarIdentifier = arrivalIdentifier;
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

    public void SetLandingTime(DateTimeOffset landingTime)
    {
        HasBeenScheduled = true;
        ScheduledLandingTime = landingTime;
    }

    public void Activate(IClock clock)
    {
        if (Activated)
            throw new MaestroException($"{Callsign} is already activated.");
        
        ActivatedTime = clock.UtcNow();
    }

    public void UpdatePosition(FlightPosition position, FixEstimate[] estimates)
    {
        LastKnownPosition = position;
        Estimates = estimates;
    }

    public void UpdateLastSeen(IClock clock)
    {
        LastSeen = clock.UtcNow();
    }
}

public enum FlowControls
{
    ProfileSpeed,
    S250
}