using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;

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

        return left.LandingTime.CompareTo(right.LandingTime);
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

    // TODO: Use memento pattern
    public Flight(FlightMessage message)
    {
        Callsign = message.Callsign;
        AircraftType = message.AircraftType;
        WakeCategory = message.WakeCategory;
        OriginIdentifier = message.OriginIdentifier;
        DestinationIdentifier = message.DestinationIdentifier;
        EstimatedDepartureTime = message.EstimatedDepartureTime;
        EstimatedTimeEnroute = message.EstimatedTimeEnroute;
        IsFromDepartureAirport = message.IsFromDepartureAirport;
        AssignedRunwayIdentifier = message.AssignedRunwayIdentifier;
        RunwayManuallyAssigned = message.RunwayManuallyAssigned;
        State = message.State;
        HighPriority = message.HighPriority;
        NoDelay = message.NoDelay;
        ActivatedTime = message.ActivatedTime;
        FeederFixIdentifier = message.FeederFixIdentifier;
        InitialFeederFixEstimate = message.InitialFeederFixEstimate;
        FeederFixEstimate = message.FeederFixEstimate;
        ManualFeederFixEstimate = message.ManualFeederFixEstimate;
        FeederFixTime = message.FeederFixTime;
        ActualFeederFixTime = message.ActualFeederFixTime;
        InitialLandingEstimate = message.InitialLandingEstimate;
        LandingEstimate = message.LandingEstimate;
        LandingTime = message.LandingTime;
        ManualLandingTime = message.ManualLandingTime;
        FlowControls = message.FlowControls;
        AssignedArrivalIdentifier = message.AssignedArrivalIdentifier;
        Fixes = message.Fixes.ToArray();
        LastSeen = message.LastSeen;
        Position = message.Position;
        IsDummy = message.IsDummy;
    }

    public string Callsign { get; }
    public string? AircraftType { get; set; }
    public WakeCategory? WakeCategory { get; set; }
    public string? OriginIdentifier { get; set; }
    public string DestinationIdentifier { get; }
    public DateTimeOffset? EstimatedDepartureTime { get; set; }
    public TimeSpan? EstimatedTimeEnroute { get; set; }

    public bool IsFromDepartureAirport { get; set; }

    // TODO: Consider storing the minimum separation required here
    // Either that or store the runway mode here so we can easily reference it later
    public string? AssignedRunwayIdentifier { get; private set; }
    public bool RunwayManuallyAssigned { get; private set; }

    public State State { get; private set; }
    public bool HighPriority { get; set; }
    public bool NoDelay { get; set; }
    public bool Activated => IsActiveState(State);
    public DateTimeOffset? ActivatedTime { get; private set; }

    public string? FeederFixIdentifier { get; private set; }
    public DateTimeOffset? InitialFeederFixEstimate { get; private set; }
    public DateTimeOffset? FeederFixEstimate { get; private set; } // ETA_FF
    public bool ManualFeederFixEstimate { get; private set; }
    public DateTimeOffset? FeederFixTime { get; private set; } // STA_FF
    public DateTimeOffset? ActualFeederFixTime { get; private set; } // ATO_FF
    public bool HasPassedFeederFix => ActualFeederFixTime is not null;

    public DateTimeOffset InitialLandingEstimate { get; private set; }
    public DateTimeOffset LandingEstimate { get; private set; } // ETA
    public DateTimeOffset LandingTime { get; private set; } // STA
    public bool ManualLandingTime { get; private set; }

    public TimeSpan TotalDelay => LandingTime - InitialLandingEstimate;
    public TimeSpan RemainingDelay => LandingTime - LandingEstimate;

    public FlowControls FlowControls { get; private set; } = FlowControls.ProfileSpeed;

    public string? AssignedArrivalIdentifier { get; set; }
    public FixEstimate[] Fixes { get; set; } = [];
    public DateTimeOffset LastSeen { get; private set; }
    public FlightPosition? Position { get; private set; }
    public bool IsDummy { get; init; }

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
        FeederFixEstimate = feederFixEstimate;
        InitialFeederFixEstimate = feederFixEstimate;
        FeederFixTime = feederFixEstimate;
        ActualFeederFixTime = actualFeederFixTime;
    }

    public void UpdateFeederFixEstimate(DateTimeOffset feederFixEstimate, bool manual = false)
    {
        if (string.IsNullOrEmpty(FeederFixIdentifier))
            throw new MaestroException("No feeder fix has been set");

        FeederFixEstimate = feederFixEstimate;
        ManualFeederFixEstimate = manual;
        if (State is State.Pending or State.New or State.Unstable)
        {
            InitialFeederFixEstimate = feederFixEstimate;
        }
    }

    public void SetFeederFixTime(DateTimeOffset feederFixTime)
    {
        if (string.IsNullOrEmpty(FeederFixIdentifier))
            throw new MaestroException("No feeder fix has been set");

        if (HasPassedFeederFix)
            throw new MaestroException(
                "Cannot update feeder fix time because the flight has already passed the feeder fix");

        FeederFixTime = feederFixTime;
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
        LandingEstimate = landingEstimate;
        if (State is State.Pending or State.New or State.Unstable)
        {
            InitialLandingEstimate = landingEstimate;
        }
    }

    public void ResetInitialLandingEstimate()
    {
        InitialLandingEstimate = LandingEstimate;
    }

    public void ResetInitialFeederFixEstimate()
    {
        InitialFeederFixEstimate = FeederFixEstimate;
    }

    public void SetLandingTime(DateTimeOffset landingTime, bool manual = false)
    {
        LandingTime = landingTime;
        ManualLandingTime = manual;
    }

    public void UpdateLastSeen(IClock clock)
    {
        LastSeen = clock.UtcNow();
    }

    public void UpdatePosition(FlightPosition? position)
    {
        Position = position;
    }

    public void MakePending()
    {
        // TODO: Prevent if the flight has departed
        // Only allowed between Preactive and Departure

        ActivatedTime = null;
        FeederFixEstimate = null;
        InitialFeederFixEstimate = null;
        FeederFixTime = null;
        LandingEstimate = default;
        InitialLandingEstimate = default;
        LandingTime = default;
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

        // Keep the flight unstable until it's passed the minimum unstable time
        var timeActive = clock.UtcNow() - ActivatedTime;
        if (State is State.Unstable && timeActive <= minUnstableTime)
        {
            return;
        }

        var now = clock.UtcNow();
        if (LandingTime.IsSameOrBefore(now))
        {
            SetState(State.Landed, clock);
            return;
        }

        var timeToLanding = LandingTime - clock.UtcNow();
        if (timeToLanding <= frozenThreshold)
        {
            SetState(State.Frozen, clock);
            return;
        }

        if (InitialFeederFixEstimate is not null && InitialFeederFixEstimate.Value.IsSameOrBefore(now))
        {
            SetState(State.SuperStable, clock);
            return;
        }

        var timeToFeeder = FeederFixEstimate - clock.UtcNow();
        if (timeToFeeder <= stableThreshold)
        {
            SetState(State.Stable, clock);
            return;
        }

        SetState(State.Unstable, clock);
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
        return $"{Callsign}; {State}; FF {FeederFixIdentifier}; ETA_FF {FeederFixEstimate:HH:mm}; STA_FF {FeederFixTime:HH:mm}; ATO_FF {ActualFeederFixTime:HH:mm}; ETA {LandingEstimate:HH:mm}; STA {LandingTime:HH:mm}";
    }
}

public enum FlowControls
{
    ProfileSpeed,
    ReduceSpeed
}
