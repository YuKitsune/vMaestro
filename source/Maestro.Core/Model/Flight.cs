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
    public string? AssignedArrivalIdentifier { get; private set; }
    public string AssignedRunwayIdentifier { get; private set; }
    public bool RunwayManuallyAssigned { get; private set; }
    public bool HighPriority { get; set; } = false;
    public bool NoDelay { get; set; }
    public bool NeedsRecompute { get; set; }

    public State State { get; private set; } = State.Unstable;
    public bool PositionIsFixed => State is not State.Unstable and not State.Stable;

    public string? FeederFixIdentifier { get; private set; }
    public DateTimeOffset? InitialFeederFixTime { get; private set; }
    public DateTimeOffset? EstimatedFeederFixTime { get; private set; } // ETA_FF
    public DateTimeOffset? ScheduledFeederFixTime { get; private set; } // STA_FF
    public DateTimeOffset? ActualFeederFixTime { get; private set; }
    public bool HasPassedFeederFix => ActualFeederFixTime is not null;

    public DateTimeOffset InitialLandingTime { get; private set; }
    public DateTimeOffset EstimatedLandingTime { get; private set; } // ETA
    public DateTimeOffset ScheduledLandingTime { get; private set; } // STA
    public TimeSpan TotalDelay => ScheduledLandingTime - InitialLandingTime;
    public TimeSpan RemainingDelay => ScheduledLandingTime - EstimatedLandingTime;
    
    public bool Activated => ActivatedTime.HasValue;
    public DateTimeOffset? ActivatedTime { get; private set; }
    
    public FlowControls FlowControls { get; private set; } = FlowControls.ProfileSpeed;
    
    public bool HasBeenScheduled { get; private set; }
    
    public DateTimeOffset LastSeen { get; private set; }

    public void SetState(State state)
    {
        if (State == State.Removed && state is not State.Removed)
            throw new MaestroException("Cannot change state as flight has been removed.");
        
        // TODO: Prevent invalid state changes
        State = state;
    }

    public void Resume()
    {
        State = State.Unstable;
    }

    public void Desequence()
    {
        State = State.Desequenced;
    }

    public void Remove()
    {
        State = State.Removed;
    }
    
    public bool ShouldSequence => State != State.Desequenced && State != State.Removed;

    public void SetArrival(string? arrivalIdentifier)
    {
        AssignedArrivalIdentifier = arrivalIdentifier;
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

    public void SetFeederFix(
        string feederFixIdentifier,
        DateTimeOffset feederFixEstimate,
        DateTimeOffset? actualFeederFixTime = null)
    {
        FeederFixIdentifier = feederFixIdentifier;
        EstimatedFeederFixTime = feederFixEstimate;
        ScheduledFeederFixTime = null;
        ActualFeederFixTime = actualFeederFixTime;
    }

    public void UpdateFeederFixEstimate(DateTimeOffset feederFixEstimate)
    {
        if (string.IsNullOrEmpty(FeederFixIdentifier))
            throw new MaestroException("No feeder fix has been set");

        if (HasPassedFeederFix)
            throw new MaestroException(
                "Cannot update feeder fix estimate because the flight has already passed the feeder fix");
        
        EstimatedFeederFixTime = feederFixEstimate;
        if (State == State.Unstable)
        {
            InitialFeederFixTime = feederFixEstimate;
        }
    }

    public void SetFeederFixTime(DateTimeOffset feederFixTime)
    {
        if (string.IsNullOrEmpty(FeederFixIdentifier))
            throw new MaestroException("No feeder fix has been set");

        if (HasPassedFeederFix)
            throw new MaestroException(
                "Cannot update feeder fix time because the flight has already passed the feeder fix");
        
        ScheduledFeederFixTime = feederFixTime;
    }

    public void PassedFeederFix(DateTimeOffset feederFixTime)
    {
        if (string.IsNullOrEmpty(FeederFixIdentifier))
            throw new MaestroException("No feeder fix has been set");

        if (HasPassedFeederFix)
            throw new MaestroException("Flight has already passed the feeder fix");
        
        ActualFeederFixTime = feederFixTime;
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

    public override string ToString()
    {
        return $"{Callsign}; {State}; FF {FeederFixIdentifier}; ETA_FF {EstimatedFeederFixTime:HH:mm}; STA_FF {ScheduledFeederFixTime:HH:mm}; ATO_FF {ActualFeederFixTime:HH:mm}; ETA {EstimatedLandingTime:HH:mm}; STA {ScheduledLandingTime:HH:mm}";
    }
}

public enum FlowControls
{
    ProfileSpeed,
    S250
}