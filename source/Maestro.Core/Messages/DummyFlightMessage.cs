using Maestro.Core.Model;

namespace Maestro.Core.Messages;

public class DummyFlightMessage
{
    public required string Callsign { get; init; }
    public required string? AircraftType { get; init; }
    public required string AssignedRunwayIdentifier { get; init; }
    public required DateTimeOffset LandingTime { get; init; }
    public required State State { get; init; }
}
