using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

public class Flight : IEquatable<Flight>
{
    /// <summary>
    /// Constructor for real flights from vatSys.
    /// </summary>
    public Flight(
        // Core identification
        string callsign,
        string aircraftType,
        AircraftCategory aircraftCategory,
        WakeCategory wakeCategory,
        string destinationIdentifier,

        // Origin info
        string? originIdentifier,
        bool isFromDepartureAirport,
        DateTimeOffset? estimatedDepartureTime,

        // Runway and trajectory
        string assignedRunwayIdentifier,
        string approachType,
        Trajectory trajectory,

        // Feeder fix info (optional - flight may not track via FF)
        string? feederFixIdentifier,
        DateTimeOffset? feederFixEstimate,

        // Landing estimate (required)
        DateTimeOffset landingEstimate,

        // Activation
        DateTimeOffset activatedTime,

        // Optional vatSys data
        FixEstimate[]? fixes = null,
        FlightPosition? position = null)
    {
        Callsign = callsign;
        AircraftType = aircraftType;
        AircraftCategory = aircraftCategory;
        WakeCategory = wakeCategory;
        DestinationIdentifier = destinationIdentifier;
        IsManuallyInserted = false;

        OriginIdentifier = originIdentifier;
        IsFromDepartureAirport = isFromDepartureAirport;
        EstimatedDepartureTime = estimatedDepartureTime;

        AssignedRunwayIdentifier = assignedRunwayIdentifier;
        ApproachType = approachType;
        Trajectory = trajectory;

        FeederFixIdentifier = feederFixIdentifier;

        // Calculate estimates based on whether flight tracks via feeder fix
        if (!string.IsNullOrEmpty(feederFixIdentifier) && feederFixEstimate.HasValue)
        {
            FeederFixEstimate = feederFixEstimate.Value;
            LandingEstimate = feederFixEstimate.Value.Add(trajectory.TimeToGo);
        }
        else
        {
            LandingEstimate = landingEstimate;
            FeederFixEstimate = landingEstimate.Subtract(trajectory.TimeToGo);
        }

        InitialFeederFixEstimate = FeederFixEstimate;
        InitialLandingEstimate = LandingEstimate;
        LandingTime = LandingEstimate;
        FeederFixTime = FeederFixEstimate;

        ActivatedTime = activatedTime;
        State = State.Unstable;

        Fixes = fixes ?? [];
        Position = position;
    }

    /// <summary>
    /// Constructor for manually inserted (dummy) flights.
    /// </summary>
    public Flight(
        string callsign,
        string aircraftType,
        AircraftCategory aircraftCategory,
        WakeCategory wakeCategory,
        string destinationIdentifier,
        string assignedRunwayIdentifier,
        string approachType,
        Trajectory trajectory,
        DateTimeOffset targetLandingTime,
        State state = State.Frozen)
    {
        Callsign = callsign;
        AircraftType = aircraftType;
        AircraftCategory = aircraftCategory;
        WakeCategory = wakeCategory;
        DestinationIdentifier = destinationIdentifier;
        IsManuallyInserted = true;

        OriginIdentifier = null;
        IsFromDepartureAirport = false;
        EstimatedDepartureTime = null;

        AssignedRunwayIdentifier = assignedRunwayIdentifier;
        ApproachType = approachType;
        Trajectory = trajectory;

        FeederFixIdentifier = null;

        LandingEstimate = targetLandingTime;
        FeederFixEstimate = targetLandingTime.Subtract(trajectory.TimeToGo);
        InitialFeederFixEstimate = FeederFixEstimate;
        InitialLandingEstimate = LandingEstimate;

        TargetLandingTime = targetLandingTime;
        LandingTime = targetLandingTime;
        FeederFixTime = FeederFixEstimate;

        State = state;

        Fixes = [];
        Position = null;
    }

    /// <summary>
    /// Constructor for deserialization from FlightDto.
    /// </summary>
    public Flight(FlightDto dto)
    {
        Callsign = dto.Callsign;
        AircraftType = dto.AircraftType;
        AircraftCategory = dto.AircraftCategory;
        WakeCategory = dto.WakeCategory;
        OriginIdentifier = dto.OriginIdentifier;
        DestinationIdentifier = dto.DestinationIdentifier;
        IsManuallyInserted = dto.IsManuallyInserted;
        EstimatedDepartureTime = dto.EstimatedDepartureTime;
        IsFromDepartureAirport = dto.IsFromDepartureAirport;

        AssignedRunwayIdentifier = dto.AssignedRunwayIdentifier
            ?? throw new ArgumentException("AssignedRunwayIdentifier required", nameof(dto));
        ApproachType = dto.ApproachType
            ?? throw new ArgumentException("ApproachType required", nameof(dto));
        Trajectory = dto.TimeToGo.HasValue
            ? new Trajectory(dto.TimeToGo.Value)
            : throw new ArgumentException("TimeToGo required", nameof(dto));

        FeederFixIdentifier = dto.FeederFixIdentifier;
        FeederFixEstimate = dto.FeederFixEstimate
            ?? throw new ArgumentException("FeederFixEstimate required", nameof(dto));
        InitialFeederFixEstimate = dto.InitialFeederFixEstimate
            ?? throw new ArgumentException("InitialFeederFixEstimate required", nameof(dto));
        ManualFeederFixEstimate = dto.ManualFeederFixEstimate;
        FeederFixTime = dto.FeederFixTime
            ?? throw new ArgumentException("FeederFixTime required", nameof(dto));

        InitialLandingEstimate = dto.InitialLandingEstimate;
        LandingEstimate = dto.LandingEstimate;
        TargetLandingTime = dto.TargetLandingTime;
        LandingTime = dto.LandingTime;

        State = dto.State;
        HighPriority = dto.HighPriority;
        MaximumDelay = dto.MaximumDelay;
        ActivatedTime = dto.ActivatedTime;
        FlowControls = dto.FlowControls;
        Fixes = dto.Fixes?.ToArray() ?? [];
        LastSeen = dto.LastSeen;
        Position = dto.Position;
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

    public string AssignedRunwayIdentifier { get; private set; }

    public State State { get; private set; }
    public bool HighPriority { get; set; }
    public TimeSpan? MaximumDelay { get; private set; }
    public DateTimeOffset? ActivatedTime { get; private set; }

    public string? FeederFixIdentifier { get; private set; }
    public DateTimeOffset InitialFeederFixEstimate { get; private set; }
    public DateTimeOffset FeederFixEstimate { get; private set; } // ETA_FF
    public bool ManualFeederFixEstimate { get; private set; }
    public DateTimeOffset FeederFixTime { get; private set; } // STA_FF

    public DateTimeOffset InitialLandingEstimate { get; private set; }
    public DateTimeOffset LandingEstimate { get; private set; } // ETA
    public DateTimeOffset? TargetLandingTime { get; private set; }
    public DateTimeOffset LandingTime { get; private set; } // STA

    public TimeSpan TotalDelay => LandingTime - InitialLandingEstimate;
    public TimeSpan RemainingDelay => LandingTime - LandingEstimate;

    public FlowControls FlowControls { get; private set; } = FlowControls.HighSpeed;

    public string ApproachType { get; private set; }
    public FixEstimate[] Fixes { get; set; } = [];
    public DateTimeOffset LastSeen { get; private set; }
    public FlightPosition? Position { get; private set; }
    public Trajectory Trajectory { get; private set; }

    public void SetState(State state, IClock clock)
    {
        State = state;
    }

    public void SetRunway(string runwayIdentifier, Trajectory trajectory)
    {
        AssignedRunwayIdentifier = runwayIdentifier;
        UpdateTrajectoryAndEstimates(trajectory);
    }

    public void SetApproachType(string approachType, Trajectory trajectory)
    {
        ApproachType = approachType;
        UpdateTrajectoryAndEstimates(trajectory);
    }

    public void UpdateFeederFixEstimate(DateTimeOffset feederFixEstimate, bool manual = false)
    {
        FeederFixEstimate = feederFixEstimate;
        ManualFeederFixEstimate = manual;

        LandingEstimate = feederFixEstimate.Add(Trajectory.TimeToGo);
        if (State is State.Unstable)
        {
            InitialFeederFixEstimate = FeederFixEstimate;
            InitialLandingEstimate = LandingEstimate;
        }
    }

    public void SetFeederFix(
        string? feederFixIdentifier,
        Trajectory trajectory,
        DateTimeOffset? feederFixEstimate,
        DateTimeOffset landingEstimate)
    {
        ManualFeederFixEstimate = false;
        FeederFixIdentifier = feederFixIdentifier;
        Trajectory = trajectory;

        // Calculate estimates based on whether flight tracks via feeder fix
        if (!string.IsNullOrEmpty(feederFixIdentifier) && feederFixEstimate.HasValue)
        {
            FeederFixEstimate = feederFixEstimate.Value;
            LandingEstimate = feederFixEstimate.Value.Add(trajectory.TimeToGo);
        }
        else
        {
            LandingEstimate = landingEstimate;
            FeederFixEstimate = landingEstimate.Subtract(trajectory.TimeToGo);
        }

        // Always reset initial estimates when feeder fix changes
        InitialFeederFixEstimate = FeederFixEstimate;
        InitialLandingEstimate = LandingEstimate;

        // Recalculate STA_FF using STA - TTG
        if (LandingTime != default)
        {
            FeederFixTime = LandingTime.Subtract(trajectory.TimeToGo);
        }
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

    private void UpdateTrajectoryAndEstimates(Trajectory trajectory)
    {
        Trajectory = trajectory;

        if (!string.IsNullOrEmpty(FeederFixIdentifier))
        {
            // Calculate ETA using ETA_FF + TTG
            LandingEstimate = FeederFixEstimate.Add(Trajectory.TimeToGo);

            if (State is State.Unstable)
                InitialLandingEstimate = LandingEstimate;
        }
        else
        {
            // If no feeder fix, calculate ETA_FF from last fix estimate - TTG
            if (Fixes.Length > 0)
            {
                var lastEstimate = Fixes.Last().Estimate;
                FeederFixEstimate = lastEstimate.Subtract(Trajectory.TimeToGo);

                if (State is State.Unstable)
                    InitialFeederFixEstimate = FeederFixEstimate;
            }
        }

        // Calculate STA_FF using STA - TTG
        if (LandingTime != default)
        {
            FeederFixTime = LandingTime.Subtract(Trajectory.TimeToGo);
        }
    }

    public void SetMaximumDelay(TimeSpan? maximumDelay)
    {
        MaximumDelay = maximumDelay;
        ClearTargetLandingTime();
    }

    public void InvalidateSequenceData()
    {
        LandingTime = LandingEstimate;
        FeederFixTime = FeederFixEstimate;
        FlowControls = FlowControls.HighSpeed;
    }

    public void Reset()
    {
        // Reset state
        State = State.Unstable;

        // Reset activation
        ActivatedTime = null;

        // Reset sequence data
        FlowControls = FlowControls.HighSpeed;

        // Reset delay controls
        MaximumDelay = null;
        TargetLandingTime = null;

        // Note: Trajectory is kept as-is and will be recalculated when re-inserted into the sequence
    }

    public void SetSequenceData(
        DateTimeOffset landingTime,
        FlowControls flowControls)
    {
        LandingTime = landingTime;
        FeederFixTime = landingTime.Subtract(Trajectory.TimeToGo);
        FlowControls = flowControls;
    }

    // TODO: Move this into Flight Updated handler
    public void UpdateStateBasedOnTime(IClock clock, AirportConfiguration airportConfiguration)
    {
        // Manually-inserted flights don't auto-update their state
        if (IsManuallyInserted)
            return;

        if (ActivatedTime is null ||
            LandingTime == default)
            return;

        // Sticky states
        if (State is State.Landed)
            return;

        var stableThreshold = TimeSpan.FromMinutes(airportConfiguration.StabilityThresholdMinutes);
        var frozenThreshold = TimeSpan.FromMinutes(airportConfiguration.FrozenThresholdMinutes);
        var minUnstableTime = TimeSpan.FromMinutes(airportConfiguration.MinimumUnstableMinutes);

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

        if (InitialFeederFixEstimate.IsSameOrBefore(now))
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
    }

    public bool Equals(Flight? other)
    {
        return other is not null && ReferenceEquals(this, other);
    }

    public override string ToString()
    {
        return $"{Callsign}: State: {State}; Runway {AssignedRunwayIdentifier}; STA {LandingTime:HH:mm};";
    }
}

