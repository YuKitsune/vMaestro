using Maestro.Contracts.Shared;
using MessagePack;

namespace Maestro.Contracts.Flights;

/// <summary>
/// Represents a flight in the Maestro sequence.
/// </summary>
[MessagePackObject]
public class FlightDto
{
    /// <summary>
    /// The flight's callsign (e.g., QFA123).
    /// </summary>
    [Key(0)]
    public required string Callsign { get; init; }

    /// <summary>
    /// The ICAO aircraft type designator (e.g., B738).
    /// </summary>
    [Key(1)]
    public required string AircraftType { get; init; }

    /// <summary>
    /// The wake turbulence category of the aircraft.
    /// </summary>
    [Key(2)]
    public required WakeCategory WakeCategory { get; init; }

    /// <summary>
    /// The category of the aircraft (e.g., jet, turboprop).
    /// </summary>
    [Key(3)]
    public required AircraftCategory AircraftCategory { get; init; }

    /// <summary>
    /// The ICAO identifier of the departure airport, if known.
    /// </summary>
    [Key(4)]
    public required string? OriginIdentifier { get; init; }

    /// <summary>
    /// The ICAO identifier of the destination airport.
    /// </summary>
    [Key(5)]
    public required string DestinationIdentifier { get; init; }

    /// <summary>
    /// Whether the flight originated from a departure airport covered by this Maestro instance.
    /// </summary>
    [Key(6)]
    public required bool IsFromDepartureAirport { get; init; }

    /// <summary>
    /// The current processing state of the flight in the sequence.
    /// </summary>
    [Key(7)]
    public required State State { get; init; }

    /// <summary>
    /// The time at which the flight was activated in Maestro.
    /// </summary>
    [Key(8)]
    public required DateTimeOffset? ActivatedTime { get; init; }

    /// <summary>
    /// Whether this flight has high priority.
    /// </summary>
    [Key(9)]
    public required bool HighPriority { get; init; }

    /// <summary>
    /// The maximum delay that can has been assigned to this flight.
    /// </summary>
    [Key(10)]
    public required TimeSpan? MaximumDelay { get; init; }

    /// <summary>
    /// The flight's position in the overall landing sequence.
    /// </summary>
    [Key(11)]
    public required int NumberInSequence { get; init; }

    /// <summary>
    /// The identifier of the feeder fix the flight is tracking via.
    /// </summary>
    [Key(12)]
    public required string? FeederFixIdentifier { get; init; }

    /// <summary>
    /// The estimated departure time if known.
    /// </summary>
    [Key(13)]
    public required DateTimeOffset? EstimatedDepartureTime { get; init; }

    /// <summary>
    /// The initial estimate for the feeder fix calculated before the flight became Stable.
    /// </summary>
    [Key(14)]
    public required DateTimeOffset? InitialFeederFixEstimate { get; init; }

    /// <summary>
    /// The current estimate for when the flight will cross the feeder fix.
    /// </summary>
    [Key(15)]
    public required DateTimeOffset? FeederFixEstimate { get; init; }

    /// <summary>
    /// Whether the feeder fix estimate was manually entered by ATC.
    /// </summary>
    [Key(16)]
    public required bool ManualFeederFixEstimate { get; init; }

    /// <summary>
    /// The scheduled time for the flight to cross the feeder fix.
    /// </summary>
    [Key(17)]
    public required DateTimeOffset? FeederFixTime { get; init; }

    /// <summary>
    /// The identifier of the runway assigned to this flight.
    /// </summary>
    [Key(19)]
    public required string AssignedRunwayIdentifier { get; init; }

    /// <summary>
    /// The flight's position in the sequence for its assigned runway.
    /// </summary>
    [Key(20)]
    public required int NumberToLandOnRunway { get; init; }

    /// <summary>
    /// The assigned approach type if any.
    /// </summary>
    [Key(21)]
    public required string ApproachType { get; init; }

    /// <summary>
    /// The initial estimated landing time before the flight became Stable.
    /// </summary>
    [Key(22)]
    public required DateTimeOffset InitialLandingEstimate { get; init; }

    /// <summary>
    /// The current estimated landing time.
    /// </summary>
    [Key(23)]
    public required DateTimeOffset LandingEstimate { get; init; }

    /// <summary>
    /// The target landing time assigned by flow control, if any.
    /// </summary>
    [Key(24)]
    public DateTimeOffset? TargetLandingTime { get; init; }

    /// <summary>
    /// The scheduled landing time assigned by Maestro.
    /// </summary>
    [Key(25)]
    public required DateTimeOffset LandingTime { get; init; }

    /// <summary>
    /// The initial delay calculated when the flight became Stable.
    /// </summary>
    [Key(26)]
    public required TimeSpan InitialDelay { get; init; }

    /// <summary>
    /// The remaining delay to be absorbed by the flight.
    /// </summary>
    [Key(27)]
    public required TimeSpan RemainingDelay { get; init; }

    /// <summary>
    /// The flow control instructions assigned to this flight.
    /// </summary>
    [Key(28)]
    public required FlowControls FlowControls { get; init; }

    /// <summary>
    /// The last time this flight's data was updated.
    /// </summary>
    [Key(29)]
    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>
    /// The current position of the flight, if known.
    /// </summary>
    [Key(30)]
    public required FlightPosition? Position { get; init; }

    /// <summary>
    /// Whether this flight was manually inserted into the sequence.
    /// </summary>
    [Key(31)]
    public required bool IsManuallyInserted { get; init; }

    /// <summary>
    /// The approximate time it will take for the flight to travel from the feeder fix to the runway threshold.
    /// </summary>
    [Key(32)]
    public TimeSpan? TimeToGo { get; init; }
}
