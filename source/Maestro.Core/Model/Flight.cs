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
        TerminalTrajectory terminalTrajectory,
        EnrouteTrajectory enrouteTrajectory,

        // Feeder fix info (optional - flight may not track via FF)
        string? feederFixIdentifier,
        DateTimeOffset? feederFixEstimate,

        // Landing estimate (required)
        DateTimeOffset landingEstimate,

        // Activation
        DateTimeOffset activatedTime,

        // Optional vatSys data
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
        EnrouteTrajectory = enrouteTrajectory;
        TerminalTrajectory = terminalTrajectory;

        FeederFixIdentifier = feederFixIdentifier;

        // Calculate estimates based on whether flight tracks via feeder fix
        if (!string.IsNullOrEmpty(feederFixIdentifier) && feederFixEstimate.HasValue)
        {
            FeederFixEstimate = feederFixEstimate.Value;
            LandingEstimate = feederFixEstimate.Value.Add(terminalTrajectory.NormalTimeToGo);
        }
        else
        {
            LandingEstimate = landingEstimate;
            FeederFixEstimate = landingEstimate.Subtract(terminalTrajectory.NormalTimeToGo);
        }

        InitialFeederFixEstimate = FeederFixEstimate;
        InitialLandingEstimate = LandingEstimate;
        LandingTime = LandingEstimate;
        FeederFixTime = FeederFixEstimate;

        ActivatedTime = activatedTime;
        State = State.Unstable;
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
        TerminalTrajectory terminalTrajectory,
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
        TerminalTrajectory = terminalTrajectory;
        EnrouteTrajectory = new EnrouteTrajectory(TimeSpan.Zero, TimeSpan.Zero);

        FeederFixIdentifier = null;

        LandingEstimate = targetLandingTime;
        FeederFixEstimate = targetLandingTime.Subtract(terminalTrajectory.NormalTimeToGo);
        InitialFeederFixEstimate = FeederFixEstimate;
        InitialLandingEstimate = LandingEstimate;

        TargetLandingTime = targetLandingTime;
        LandingTime = targetLandingTime;
        FeederFixTime = FeederFixEstimate;

        State = state;

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

        AssignedRunwayIdentifier = dto.AssignedRunwayIdentifier;
        ApproachType = dto.ApproachType;
        TerminalTrajectory = new TerminalTrajectory(dto.TerminalNormalTimeToGo, dto.TerminalPressureTimeToGo, dto.TerminalMaxPressureTimeToGo);
        EnrouteTrajectory = new EnrouteTrajectory(dto.EnrouteMaxLinearDelay, dto.EnrouteShortcutTimeToGain);

        FeederFixIdentifier = dto.FeederFixIdentifier;
        FeederFixEstimate = dto.FeederFixEstimate;
        InitialFeederFixEstimate = dto.InitialFeederFixEstimate;
        ManualFeederFixEstimate = dto.ManualFeederFixEstimate;
        FeederFixTime = dto.FeederFixTime;

        InitialLandingEstimate = dto.InitialLandingEstimate;
        LandingEstimate = dto.LandingEstimate;
        TargetLandingTime = dto.TargetLandingTime;
        LandingTime = dto.LandingTime;

        State = dto.State;
        HighPriority = dto.HighPriority;
        MaximumDelay = dto.MaximumDelay;
        RequiredEnrouteDelay = dto.RequiredEnrouteDelay;
        RequiredTerminalDelay = dto.RequiredTerminalDelay;
        RemainingEnrouteDelay = dto.RemainingEnrouteDelay;
        RemainingTerminalDelay = dto.RemainingTerminalDelay;
        RequiredControlAction = dto.RequiredControlAction;
        RemainingControlAction = dto.RemainingControlAction;
        ActivatedTime = dto.ActivatedTime;
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

    public State State { get; private set; }
    public bool HighPriority { get; set; }
    public DateTimeOffset? ActivatedTime { get; private set; }
    public DateTimeOffset LastSeen { get; private set; }

    public string? FeederFixIdentifier { get; private set; }
    public DateTimeOffset InitialFeederFixEstimate { get; private set; }
    public DateTimeOffset FeederFixEstimate { get; private set; } // ETA_FF
    public bool ManualFeederFixEstimate { get; private set; }
    public DateTimeOffset FeederFixTime { get; private set; } // STA_FF

    public string AssignedRunwayIdentifier { get; private set; }
    public string ApproachType { get; private set; }
    public TerminalTrajectory TerminalTrajectory { get; private set; }
    public EnrouteTrajectory EnrouteTrajectory { get; private set; }

    public DateTimeOffset InitialLandingEstimate { get; private set; }
    public DateTimeOffset LandingEstimate { get; private set; } // ETA
    public DateTimeOffset? TargetLandingTime { get; private set; }
    public DateTimeOffset LandingTime { get; private set; } // STA

    public TimeSpan? MaximumDelay { get; private set; }
    public TimeSpan RequiredEnrouteDelay { get; private set; }
    public TimeSpan RequiredTerminalDelay { get; private set; }
    public TimeSpan RemainingEnrouteDelay { get; private set; }
    public TimeSpan RemainingTerminalDelay { get; private set; }
    public ControlAction RequiredControlAction { get; private set; } = ControlAction.NoDelay;
    public ControlAction RemainingControlAction { get; private set; } = ControlAction.NoDelay;
    public bool HighSpeed => RequiredEnrouteDelay < TimeSpan.FromMinutes(1);

    public FlightPosition? Position { get; private set; }

    public void SetState(State state, IClock clock)
    {
        State = state;
    }

    public void SetRunway(string runwayIdentifier, TerminalTrajectory terminalTrajectory)
    {
        AssignedRunwayIdentifier = runwayIdentifier;
        SetTrajectory(terminalTrajectory);
    }

    public void SetApproachType(string approachType, TerminalTrajectory terminalTrajectory)
    {
        ApproachType = approachType;
        SetTrajectory(terminalTrajectory);
    }

    public void SetTrajectory(TerminalTrajectory terminalTrajectory)
    {
        UpdateTrajectoryAndEstimates(terminalTrajectory);
    }

    public void UpdateFeederFixEstimate(DateTimeOffset feederFixEstimate, bool manual = false)
    {
        FeederFixEstimate = feederFixEstimate;
        ManualFeederFixEstimate = manual;

        LandingEstimate = feederFixEstimate.Add(TerminalTrajectory.NormalTimeToGo);
        if (State is State.Unstable)
        {
            InitialFeederFixEstimate = FeederFixEstimate;
            InitialLandingEstimate = LandingEstimate;
        }
    }

    public void UpdateLandingEstimate(DateTimeOffset landingEstimate)
    {
        LandingEstimate = landingEstimate;
        FeederFixEstimate = LandingEstimate.Subtract(TerminalTrajectory.NormalTimeToGo);

        if (State is State.Unstable)
        {
            InitialFeederFixEstimate = FeederFixEstimate;
            InitialLandingEstimate = LandingEstimate;
        }
    }

    public void SetFeederFix(
        string? feederFixIdentifier,
        TerminalTrajectory terminalTrajectory,
        DateTimeOffset? feederFixEstimate,
        DateTimeOffset landingEstimate)
    {
        ManualFeederFixEstimate = false;
        FeederFixIdentifier = feederFixIdentifier;
        TerminalTrajectory = terminalTrajectory;

        // Calculate estimates based on whether flight tracks via feeder fix
        if (!string.IsNullOrEmpty(feederFixIdentifier) && feederFixEstimate.HasValue)
        {
            FeederFixEstimate = feederFixEstimate.Value;
            LandingEstimate = feederFixEstimate.Value.Add(terminalTrajectory.NormalTimeToGo);
        }
        else
        {
            LandingEstimate = landingEstimate;
            FeederFixEstimate = landingEstimate.Subtract(terminalTrajectory.NormalTimeToGo);
        }

        // Always reset initial estimates when feeder fix changes
        InitialFeederFixEstimate = FeederFixEstimate;
        InitialLandingEstimate = LandingEstimate;

        // Recalculate STA_FF using STA - TTG
        if (LandingTime != default)
        {
            FeederFixTime = LandingTime.Subtract(terminalTrajectory.NormalTimeToGo);
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

    private void UpdateTrajectoryAndEstimates(TerminalTrajectory terminalTrajectory)
    {
        TerminalTrajectory = terminalTrajectory;

        if (!string.IsNullOrEmpty(FeederFixIdentifier))
        {
            // Calculate ETA using ETA_FF + TTG
            // Note: Don't update InitialLandingEstimate here - it should only change
            // when external estimates change (feeder fix or landing estimate updates),
            // not when the trajectory changes due to runway/approach reassignment
            LandingEstimate = FeederFixEstimate.Add(TerminalTrajectory.NormalTimeToGo);
        }
        else
        {
            // Calculate ETA_FF from ETA - TTG
            FeederFixEstimate = LandingEstimate.Subtract(TerminalTrajectory.NormalTimeToGo);
        }

        // Recalculate STA_FF using STA - TTG to preserve the landing time
        if (LandingTime != default)
        {
            FeederFixTime = LandingTime.Subtract(TerminalTrajectory.NormalTimeToGo);
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
        RequiredEnrouteDelay = TimeSpan.Zero;
        RequiredTerminalDelay = TimeSpan.Zero;
        RemainingEnrouteDelay = TimeSpan.Zero;
        RemainingTerminalDelay = TimeSpan.Zero;
    }

    public void SetSequenceData(
        DateTimeOffset landingTime,
        DateTimeOffset feederFixTime,
        ControlAction requiredControlAction,
        TimeSpan enrouteDelay,
        TimeSpan terminalDelay)
    {
        LandingTime = landingTime;
        FeederFixTime = feederFixTime;
        RequiredControlAction = requiredControlAction;
        RemainingControlAction = requiredControlAction;
        RequiredEnrouteDelay = enrouteDelay;
        RequiredTerminalDelay = terminalDelay;
        RemainingEnrouteDelay = enrouteDelay;
        RemainingTerminalDelay = terminalDelay;
    }

    public void SetRemainingControlAction(ControlAction remainingControlAction)
    {
        RemainingControlAction = remainingControlAction;
    }

    public void SetRemainingDelayData(DelayDistribution distribution)
    {
        RemainingEnrouteDelay = distribution.EnrouteDelay;
        RemainingTerminalDelay = distribution.TerminalDelay;
        RemainingControlAction = distribution.ControlAction;
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

