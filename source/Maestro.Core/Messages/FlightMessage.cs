using Maestro.Core.Model;

namespace Maestro.Core.Messages;

public class FlightMessage
{
    public required string Callsign { get; init; }
    public required string? AircraftType { get; init; }
    public required WakeCategory? WakeCategory { get; init; }
    public required string? OriginIdentifier { get; init; }
    public required string DestinationIdentifier { get; init; }
    public required bool IsFromDepartureAirport { get; init; }
    public required State State { get; init; }
    public required int NumberInSequence { get; init; }
    public required string? FeederFixIdentifier { get; init; }
    public required DateTimeOffset? EstimatedDepartureTime { get; init; }
    public required DateTimeOffset? InitialFeederFixEstimate { get; init; }
    public required DateTimeOffset? FeederFixEstimate { get; init; }
    public required DateTimeOffset? FeederFixTime { get; init; }
    public required string? AssignedRunway { get; init; }
    public required int NumberToLandOnRunway { get; init; }
    public required DateTimeOffset InitialLandingEstimate { get; init; }
    public required DateTimeOffset LandingEstimate { get; init; }
    public required DateTimeOffset LandingTime { get; init; }
    public required TimeSpan InitialDelay { get; init; }
    public required TimeSpan RemainingDelay { get; init; }
    public required FlowControls FlowControls { get; init; }
}
