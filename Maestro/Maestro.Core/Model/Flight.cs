using System.Diagnostics;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

public sealed class FlightComparer : IComparer<Flight>
{
    public static FlightComparer Instance { get; } = new();
    
    public int Compare(Flight? left, Flight? right)
    {
        if (left is null)
            return -1;
            
        if (right is null)
            return 1;
        
        var timeComparison = left.ScheduledLandingTime.CompareTo(right.ScheduledLandingTime);
        if (timeComparison != 0)
            return timeComparison;
        
        // Compare by state descending
        var stateComparison = right.State.CompareTo(left.State);
        if (stateComparison != 0)
            return stateComparison;
        
        // To prevent two flights from sharing the same position if they have the same state, estimate, and scheduled
        // times, use their callsigns to differentiate.
        return string.Compare(left.Callsign, right.Callsign, StringComparison.Ordinal);
    }
}

[DebuggerDisplay("{Callsign} {ScheduledLandingTime}")]
public class Flight : IEquatable<Flight>, IComparable<Flight>
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
    public bool RunwayManuallyAssigned { get; private set; }
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

    public void SetRunway(string runwayIdentifier, bool manual)
    {
        AssignedRunwayIdentifier = runwayIdentifier;
        RunwayManuallyAssigned = manual;
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
    
    public int CompareTo(Flight? other)
    {
        return FlightComparer.Instance.Compare(this, other);
    }

    public bool Equals(Flight? other)
    {
        return other is not null && (ReferenceEquals(this, other) || Callsign == other.Callsign); 
    }
}

public enum FlowControls
{
    ProfileSpeed,
    S250
}