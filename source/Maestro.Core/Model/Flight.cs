using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public class Flight : IEquatable<Flight>
{
    public Flight(string callsign, string destinationIdentifier, DateTimeOffset initialLandingEstimate, DateTimeOffset activatedTime, string aircraftType, AircraftCategory aircraftCategory, WakeCategory wakeCategory)
    {
        Callsign = callsign;
        DestinationIdentifier = destinationIdentifier;
        State = State.Unstable;
        ActivatedTime = activatedTime;
        AircraftType = aircraftType;
        AircraftCategory = aircraftCategory;
        WakeCategory = wakeCategory;
        UpdateLandingEstimate(initialLandingEstimate);
        IsManuallyInserted = false;
    }

    // Constructor for manually inserted flights (formerly DummyFlight)
    public Flight(
        string callsign,
        string aircraftType,
        AircraftCategory aircraftCategory,
        WakeCategory wakeCategory,
        string destinationIdentifier,
        string runwayIdentifier,
        DateTimeOffset targetTime,
        State state)
    {
        Callsign = callsign;
        AircraftType = aircraftType;
        AircraftCategory = aircraftCategory;
        WakeCategory = wakeCategory;
        DestinationIdentifier = destinationIdentifier;
        AssignedRunwayIdentifier = runwayIdentifier;
        InitialLandingEstimate = targetTime;
        LandingEstimate = targetTime;
        LandingTime = targetTime;
        State = state;
        IsManuallyInserted = true;
    }

    // TODO: Use memento pattern
    public Flight(FlightMessage message)
    {
        Callsign = message.Callsign;
        AircraftType = message.AircraftType;
        AircraftCategory = message.AircraftCategory;
        WakeCategory = message.WakeCategory;
        OriginIdentifier = message.OriginIdentifier;
        DestinationIdentifier = message.DestinationIdentifier;
        EstimatedDepartureTime = message.EstimatedDepartureTime;
        IsFromDepartureAirport = message.IsFromDepartureAirport;
        AssignedRunwayIdentifier = message.AssignedRunwayIdentifier;
        RunwayManuallyAssigned = message.RunwayManuallyAssigned;
        State = message.State;
        HighPriority = message.HighPriority;
        MaximumDelay = message.MaximumDelay;
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
        FlowControls = message.FlowControls;
        ApproachType = message.ApproachType;
        Fixes = message.Fixes.ToArray();
        LastSeen = message.LastSeen;
        Position = message.Position;
        IsManuallyInserted = message.IsManuallyInserted;
    }

    public string Callsign { get; }
    public string AircraftType { get; set; }
    public AircraftCategory AircraftCategory { get; set; }
    public WakeCategory WakeCategory { get; set; }
    public string? OriginIdentifier { get; set; }
    public string DestinationIdentifier { get; }
    public bool IsManuallyInserted { get; }
    public DateTimeOffset? EstimatedDepartureTime { get; set; }

    public bool IsFromDepartureAirport { get; set; }

    // TODO: Consider storing the minimum separation required here
    // Either that or store the runway mode here so we can easily reference it later
    public string? AssignedRunwayIdentifier { get; private set; }
    public bool RunwayManuallyAssigned { get; private set; }

    public State State { get; private set; }
    public bool HighPriority { get; set; }
    public TimeSpan? MaximumDelay { get; private set; }
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
    public DateTimeOffset? TargetLandingTime { get; private set; }
    public DateTimeOffset LandingTime { get; private set; } // STA

    public TimeSpan TotalDelay => LandingTime - InitialLandingEstimate;
    public TimeSpan RemainingDelay => LandingTime - LandingEstimate;

    public FlowControls FlowControls { get; private set; } = FlowControls.ProfileSpeed;

    public string ApproachType { get; private set; }
    public FixEstimate[] Fixes { get; set; } = [];
    public DateTimeOffset LastSeen { get; private set; }
    public FlightPosition? Position { get; private set; }

    public void SetState(State state, IClock clock)
    {
        State = state;
    }

    public void SetRunway(string runwayIdentifier, bool manual)
    {
        AssignedRunwayIdentifier = runwayIdentifier;
        RunwayManuallyAssigned = manual;
    }

    public void SetFeederFix(
        string feederFixIdentifier,
        DateTimeOffset feederFixEstimate,
        DateTimeOffset? actualFeederFixTime = null)
    {
        FeederFixIdentifier = feederFixIdentifier;
        FeederFixEstimate = feederFixEstimate;
        InitialFeederFixEstimate = feederFixEstimate;
        ActualFeederFixTime = actualFeederFixTime;
    }

    public void SetApproachType(string approachType)
    {
        ApproachType = approachType;
    }

    public void UpdateFeederFixEstimate(DateTimeOffset feederFixEstimate, bool manual = false)
    {
        if (string.IsNullOrEmpty(FeederFixIdentifier))
            throw new MaestroException("No feeder fix has been set");

        FeederFixEstimate = feederFixEstimate;
        ManualFeederFixEstimate = manual;
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
    }

    public void SetTargetLandingTime(DateTimeOffset targetLandingTime)
    {
        MaximumDelay = null;
        TargetLandingTime = targetLandingTime;
    }

    public void ClearTargetLandingTime()
    {
        TargetLandingTime = null;
    }

    public void UpdateLastSeen(IClock clock)
    {
        LastSeen = clock.UtcNow();
    }

    public void UpdatePosition(FlightPosition? position)
    {
        Position = position;
    }

    public void SetMaximumDelay(TimeSpan? maximumDelay)
    {
        MaximumDelay = maximumDelay;
        ClearTargetLandingTime();
    }

    public void InvalidateSequenceData()
    {
        InitialLandingEstimate = LandingEstimate;
        InitialFeederFixEstimate = FeederFixEstimate;

        LandingTime = LandingEstimate;
        FeederFixTime = FeederFixEstimate;
        FlowControls = FlowControls.ProfileSpeed;
    }

    public void SetSequenceData(
        DateTimeOffset landingTime,
        DateTimeOffset? feederFixTime,
        FlowControls flowControls)
    {
        InitialLandingEstimate = LandingEstimate;
        InitialFeederFixEstimate = FeederFixEstimate;

        LandingTime = landingTime;
        FeederFixTime = feederFixTime;
        FlowControls = flowControls;
    }

    public void Reset()
    {
        // TODO: Prevent if the flight has departed
        // Only allowed between Preactive and Departure

        ActivatedTime = null;
        FeederFixEstimate = null;
        InitialFeederFixEstimate = null;
        FeederFixTime = null;
        AssignedRunwayIdentifier = null;
        RunwayManuallyAssigned = false;
        LandingEstimate = default;
        TargetLandingTime = default;
        InitialLandingEstimate = default;
        LandingTime = default;
        State = State.Unstable;
        FlowControls = FlowControls.ProfileSpeed;
        MaximumDelay = null;
    }

    public void UpdateStateBasedOnTime(IClock clock)
    {
        // Manually-inserted flights don't auto-update their state
        if (IsManuallyInserted)
            return;

        if (ActivatedTime is null ||
            LandingTime == default ||
            InitialFeederFixEstimate is null ||
            FeederFixEstimate is null)
            return;

        // Sticky states
        if (State is State.Landed)
            return;

        // TODO: Make configurable
        var stableThreshold = TimeSpan.FromMinutes(25);
        var frozenThreshold = TimeSpan.FromMinutes(15);
        var minUnstableTime = TimeSpan.FromSeconds(180);

        // Keep the flight unstable until it's passed the minimum unstable time
        var timeActive = clock.UtcNow() - ActivatedTime;
        if (ActivatedTime is null || State is State.Unstable && timeActive <= minUnstableTime)
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

        // SetState(State.Unstable, clock);
    }

    public bool Equals(Flight? other)
    {
        return other is not null && (ReferenceEquals(this, other) || Callsign == other.Callsign);
    }

    public override string ToString()
    {
        return $"{Callsign}: State: {State}; Runway {AssignedRunwayIdentifier ?? "null"}; STA {LandingTime:HH:mm};";
    }
}

public enum FlowControls
{
    ProfileSpeed,
    ReduceSpeed
}
