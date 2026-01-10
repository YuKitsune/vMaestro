using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class ArrivalConfiguration
{
    public required string FeederFix { get; init; }
    public string ApproachType { get; init; } = string.Empty;
    public string ApproachFix { get; init; } = string.Empty;
    public AircraftCategory? Category { get; init; }
    public string[] AdditionalAircraftTypes { get; init; } = [];
    public string AircraftType { get; init; } = string.Empty;

    // TODO: Distance-based calculations
    public required IReadOnlyDictionary<string, int> RunwayIntervals { get; init; }
}
