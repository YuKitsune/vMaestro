using Maestro.Contracts.Shared;

namespace Maestro.Contracts.Flights;

/// <summary>
/// Represents a flight in the Maestro sequence.
/// </summary>
public class FlightDto
{
    /// <summary>
    /// The flight's callsign (e.g., QFA123).
    /// </summary>
    public required string Callsign { get; init; }

    /// <summary>
    /// The ICAO aircraft type designator (e.g., B738).
    /// </summary>
    public required string AircraftType { get; init; }

    /// <summary>
    /// The wake turbulence category of the aircraft.
    /// </summary>
    public required WakeCategory WakeCategory { get; init; }

    /// <summary>
    /// The category of the aircraft (e.g., jet, turboprop).
    /// </summary>
    public required AircraftCategory AircraftCategory { get; init; }

    /// <summary>
    /// The ICAO identifier of the departure airport, if known.
    /// </summary>
    public required string? OriginIdentifier { get; init; }

    /// <summary>
    /// The ICAO identifier of the destination airport.
    /// </summary>
    public required string DestinationIdentifier { get; init; }

    /// <summary>
    /// Whether the flight originated from a departure airport covered by this Maestro instance.
    /// </summary>
    public required bool IsFromDepartureAirport { get; init; }

    /// <summary>
    /// The current processing state of the flight in the sequence.
    /// </summary>
    public required State State { get; init; }

    /// <summary>
    /// The time at which the flight was activated in Maestro.
    /// </summary>
    public required DateTimeOffset? ActivatedTime { get; init; }

    /// <summary>
    /// Whether this flight has high priority.
    /// </summary>
    public required bool HighPriority { get; init; }

    /// <summary>
    /// The maximum delay that can has been assigned to this flight.
    /// </summary>
    public required TimeSpan? MaximumDelay { get; init; }

    /// <summary>
    /// The flight's position in the overall landing sequence.
    /// </summary>
    public required int NumberInSequence { get; init; }

    /// <summary>
    /// The identifier of the feeder fix the flight is tracking via.
    /// </summary>
    public required string? FeederFixIdentifier { get; init; }

    /// <summary>
    /// The estimated departure time if known.
    /// </summary>
    public required DateTimeOffset? EstimatedDepartureTime { get; init; }

    /// <summary>
    /// The initial estimate for the feeder fix calculated before the flight became Stable.
    /// </summary>
    public required DateTimeOffset? InitialFeederFixEstimate { get; init; }

    /// <summary>
    /// The current estimate for when the flight will cross the feeder fix.
    /// </summary>
    public required DateTimeOffset? FeederFixEstimate { get; init; }

    /// <summary>
    /// Whether the feeder fix estimate was manually entered by ATC.
    /// </summary>
    public required bool ManualFeederFixEstimate { get; init; }

    /// <summary>
    /// The scheduled time for the flight to cross the feeder fix.
    /// </summary>
    public required DateTimeOffset? FeederFixTime { get; init; }

    /// <summary>
    /// The actual time the flight crossed the feeder fix.
    /// </summary>
    public required DateTimeOffset? ActualFeederFixTime { get; init; }

    /// <summary>
    /// The identifier of the runway assigned to this flight.
    /// </summary>
    public required string AssignedRunwayIdentifier { get; init; }

    /// <summary>
    /// The flight's position in the sequence for its assigned runway.
    /// </summary>
    public required int NumberToLandOnRunway { get; init; }

    /// <summary>
    /// The assigned approach type if any.
    /// </summary>
    public required string ApproachType { get; init; }

    /// <summary>
    /// The initial estimated landing time before the flight became Stable.
    /// </summary>
    public required DateTimeOffset InitialLandingEstimate { get; init; }

    /// <summary>
    /// The current estimated landing time.
    /// </summary>
    public required DateTimeOffset LandingEstimate { get; init; }

    /// <summary>
    /// The target landing time assigned by flow control, if any.
    /// </summary>
    public DateTimeOffset? TargetLandingTime { get; init; }

    /// <summary>
    /// The scheduled landing time assigned by Maestro.
    /// </summary>
    public required DateTimeOffset LandingTime { get; init; }

    /// <summary>
    /// The initial delay calculated when the flight became Stable.
    /// </summary>
    public required TimeSpan InitialDelay { get; init; }

    /// <summary>
    /// The remaining delay to be absorbed by the flight.
    /// </summary>
    public required TimeSpan RemainingDelay { get; init; }

    /// <summary>
    /// The flow control instructions assigned to this flight.
    /// </summary>
    public required FlowControls FlowControls { get; init; }

    /// <summary>
    /// The last time this flight's data was updated.
    /// </summary>
    public required DateTimeOffset LastSeen { get; init; }

    /// <summary>
    /// Estimates for each fix along the flight's route.
    /// </summary>
    public required FixEstimate[] Fixes { get; init; } = [];

    /// <summary>
    /// The current position of the flight, if known.
    /// </summary>
    public required FlightPosition? Position { get; init; }

    /// <summary>
    /// Whether this flight was manually inserted into the sequence.
    /// </summary>
    public required bool IsManuallyInserted { get; init; }

    /// <summary>
    /// The approximate time it will take for the flight to travel from the feeder fix to the runway threshold.
    /// </summary>
    public TimeSpan? TimeToGo { get; init; }
}
