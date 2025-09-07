using System.Text.RegularExpressions;
using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

// TODO: Transitions

public class ArrivalConfiguration
{
    public required string FeederFix { get; init; }
    public required Regex ArrivalRegex { get; init; }
    public AircraftCategory? Category { get; init; }
    public string[] AdditionalAircraftTypes { get; init; } = [];
    public string AircraftType { get; init; } = string.Empty;

    // TODO: Distance-based calculations.
    public required IReadOnlyDictionary<string, int> RunwayIntervals { get; init; }
}
