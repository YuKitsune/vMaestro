using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class ArrivalConfiguration
{
    public required string AirportIdentifier { get; init; }

    public required string FeederFixIdentifier { get; init; }
    public string TransitionFixIdentifier { get; init; } = string.Empty;

    public required string RunwayIdentifier { get; init; }
    public string ApproachType { get; init; } = string.Empty;

    public required AircraftCategory Category { get; init; }
    public string[] AircraftTypes { get; init; } = [];

    public required TimeSpan TimeToGo { get; init; }
    public int TrackMiles { get; init; }
}
