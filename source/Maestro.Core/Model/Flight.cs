using Maestro.Core.Extensions;
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

        return left.ScheduledLandingTime.CompareTo(right.ScheduledLandingTime);
    }
}

public class Flight : IEquatable<Flight>, IComparable<Flight>
{
    public Flight(string callsign, string destinationIdentifier, DateTimeOffset initialLandingEstimate)
    {
        Callsign = callsign;
        DestinationIdentifier = destinationIdentifier;
        State = State.New;
        UpdateLandingEstimate(initialLandingEstimate);
    }

    public string Callsign { get; }
    public string? AircraftType { get; set; }
    public WakeCategory? WakeCategory { get; set; }
    public string? OriginIdentifier { get; set; }
    public string DestinationIdentifier { get; }
    public DateTimeOffset? EstimatedDepartureTime { get; set; }
    public TimeSpan? EstimatedTimeEnroute { get; set; }

    public bool IsFromDepartureAirport { get; set; }

    public string? AssignedRunwayIdentifier { get; private set; }
    public bool RunwayManuallyAssigned { get; private set; }

    public State State { get; private set; }
    public bool HighPriority { get; set; }
    public bool NoDelay { get; set; }
    public bool NeedsRecompute { get; set; }
    public bool Activated => IsActiveState(State);
    public DateTimeOffset? ActivatedTime { get; private set; }

    public string? FeederFixIdentifier { get; private set; }
    public DateTimeOffset? InitialFeederFixTime { get; private set; }
    public DateTimeOffset? EstimatedFeederFixTime { get; private set; } // ETA_FF
    public DateTimeOffset? ScheduledFeederFixTime { get; private set; } // STA_FF
    public DateTimeOffset? ActualFeederFixTime { get; private set; } // ATO_FF
    public bool HasPassedFeederFix => ActualFeederFixTime is not null;

    public DateTimeOffset InitialLandingTime { get; private set; }
    public DateTimeOffset EstimatedLandingTime { get; private set; } // ETA
    public DateTimeOffset ScheduledLandingTime { get; private set; } // STA
    public bool ManualLandingTime { get; private set; }

    /// <summary>
    ///     Used to insert flights at a specific target time without affecting the scheduled time.
    ///     This is useful when inserting dummy or uncoupled flights where a landing estimate won't be available.
    /// </summary>
    public DateTimeOffset? TargetLandingTime { get; private set; }

    public TimeSpan TotalDelay => ScheduledLandingTime - InitialLandingTime;
    public TimeSpan RemainingDelay => ScheduledLandingTime - EstimatedLandingTime;

    public FlowControls FlowControls { get; private set; } = FlowControls.ProfileSpeed;

    public string? AssignedArrivalIdentifier { get; set; }
    public FixEstimate[] Fixes { get; set; } = [];
    public DateTimeOffset LastSeen { get; private set; }

    public void SetState(State state, IClock clock)
    {
        if (State == State.Removed && state is not State.Removed)
            throw new MaestroException("Cannot change state as flight has been removed.");

        if (!IsActiveState(State) && IsActiveState(state))
            ActivatedTime = clock.UtcNow();

        // TODO: Prevent invalid state changes
        State = state;
    }

    bool IsActiveState(State state) => state is State.Unstable or State.Stable or State.SuperStable or State.Frozen or State.Landed;

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

    public void SetRunway(string runwayIdentifier, bool manual)
    {
        AssignedRunwayIdentifier = runwayIdentifier;
        RunwayManuallyAssigned = manual;
    }

    public void ClearRunway()
    {
        AssignedRunwayIdentifier = null;
        RunwayManuallyAssigned = false;
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
        InitialFeederFixTime = feederFixEstimate;
        ScheduledFeederFixTime = feederFixEstimate;
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
        if (State is State.Pending or State.New or State.Unstable)
        {
            InitialLandingTime = landingEstimate;
        }
    }

    public void SetLandingTime(DateTimeOffset landingTime, bool manual = false)
    {
        ScheduledLandingTime = landingTime;
        ManualLandingTime = manual;
        TargetLandingTime = null;
    }

    public void SetTargetTime(DateTimeOffset targetTime)
    {
        TargetLandingTime = targetTime;
    }

    public void UpdateLastSeen(IClock clock)
    {
        LastSeen = clock.UtcNow();
    }

    public void MakePending()
    {
        // TODO: Prevent if the flight has departed
        // Only allowed between Preactive and Departure

        ActivatedTime = null;
        EstimatedFeederFixTime = null;
        InitialFeederFixTime = null;
        ScheduledFeederFixTime = null;
        EstimatedLandingTime = default;
        InitialLandingTime = default;
        ScheduledLandingTime = default;
        ManualLandingTime = false;
        State = State.Pending;
    }

    public void UpdateStateBasedOnTime(IClock clock)
    {
        // Sticky states
        if (State is State.Pending or State.Landed or State.Desequenced or State.Removed)
            return;

        // TODO: Make configurable
        var stableThreshold = TimeSpan.FromMinutes(25);
        var frozenThreshold = TimeSpan.FromMinutes(15);
        var minUnstableTime = TimeSpan.FromSeconds(180);

        var timeActive = clock.UtcNow() - ActivatedTime;
        var timeToFeeder = EstimatedFeederFixTime - clock.UtcNow();
        var timeToLanding = EstimatedLandingTime - clock.UtcNow();

        // Keep the flight unstable until it's passed the minimum unstable time
        if (State is State.Unstable && timeActive <= minUnstableTime)
        {
            return;
        }

        if (ScheduledLandingTime.IsSameOrBefore(clock.UtcNow()))
        {
            SetState(State.Landed, clock);
        }
        else if (timeToLanding <= frozenThreshold)
        {
            SetState(State.Frozen, clock);
        }
        else if (InitialFeederFixTime?.IsSameOrBefore(clock.UtcNow()) ?? false)
        {
            SetState(State.SuperStable, clock);
        }
        else if (timeToFeeder <= stableThreshold)
        {
            SetState(State.Stable, clock);
        }
        else
        {
            // No change required
            return;
        }
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
    ReduceSpeed
}
