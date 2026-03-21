using Maestro.Contracts.Shared;

namespace Maestro.Core.Configuration;

public class AirportConfiguration
{
    /// <summary>
    ///     The ICAO code of the airport.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     The feeder fixes available at this airport.
    /// </summary>
    public required string[] FeederFixes { get; init; }

    /// <summary>
    ///     The runways available at this airport.
    /// </summary>
    public required string[] Runways { get; init; }

    // Defaults

    /// <summary>
    ///     The default aircraft type to use when the aircraft type cannot be determined for a flight,
    ///     or when inserting a flight manually, and no aircraft type is specified.
    /// </summary>
    public string DefaultAircraftType { get; init; } = "B738";

    /// <summary>
    ///     The default <see cref="State"/> of pending flights on insertion into the sequence.
    /// </summary>
    public State DefaultPendingFlightState { get; init; } = State.Stable;

    /// <summary>
    ///     The default <see cref="State"/> of departures on insertion into the sequence.
    /// </summary>
    public State DefaultDepartureFlightState { get; init; } = State.Unstable;

    /// <summary>
    ///     The default <see cref="State"/> of dummy flights on insertion into the sequence.
    /// </summary>
    public State DefaultDummyFlightState { get; init; } = State.Frozen;

    /// <summary>
    ///     The <see cref="State"/> to move flights into once manual interaction has been performed by the controller.
    /// </summary>
    public State ManualInteractionState { get; init; } = State.Stable;

    // State transition times

    /// <summary>
    ///     The maximum amount of time a flight must be away from landing before it is tracked by Maestro.
    /// </summary>
    public int FlightCreationThresholdMinutes { get; init; } = 120;

    /// <summary>
    ///     The minimum amount of time a flight must be considered <see cref="State.Unstable"/> before it may progress to <see cref="State.Stable"/>.
    /// </summary>
    public int MinimumUnstableMinutes { get; init; } = 5;

    /// <summary>
    ///     The amount of time before the STA_FF that a flight will transition to the <see cref="State.Stable"/> state.
    /// </summary>
    public int StabilityThresholdMinutes { get; init; } = 25;

    /// <summary>
    ///     The amount of time before the STA that a flight will transition to the <see cref="State.Frozen"/> state.
    /// </summary>
    public int FrozenThresholdMinutes { get; init; } = 15;

    // Retention

    /// <summary>
    ///     The maximum number of <see cref="State.Landed"/> flights to retain after landing.
    /// </summary>
    public int MaxLandedFlights { get; init; } = 5;

    /// <summary>
    ///     The maximum amount of time before <see cref="State.Landed"/> flights are removed.
    /// </summary>
    public int LandedFlightTimeoutMinutes { get; init; } = 10;

    /// <summary>
    ///     The maximum amount of time before lost flights are removed.
    /// </summary>
    public int LostFlightTimeoutMinutes { get; init; } = 10;

    public required RunwayModeConfiguration[] RunwayModes { get; init; }

    // Look-up tables
    public required TrajectoryConfiguration[] Trajectories { get; init; }
    public required DepartureConfiguration[] DepartureAirports { get; init; }

    // TODO: Average taxi times and terminal assignments

    // Everything beyond this point is purely for presentation, and not used within Maestro.Core

    // Presentation

    /// <summary>
    /// The average speed (TAS) of aircraft on final
    /// </summary>
    public int AverageLandingSpeed { get; init; } = 150;

    /// <summary>
    /// The altitude in feet of the upper winds to load from GRIB
    /// </summary>
    public int UpperWindAltitude { get; init; } = 6000;

    public AirportColourConfiguration? Colours { get; init; }
    public required ViewConfiguration[] Views { get; init; }

    // Coordination Messages
    public required string[] GlobalCoordinationMessages { get; init; }
    public required string[] FlightCoordinationMessages { get; init; }
}
