using System.Text.RegularExpressions;
using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class ArrivalConfiguration
{
    public required string FeederFix { get; init; }
    public required Regex ArrivalRegex { get; init; }
    public AircraftCategory? Category { get; init; }
    public string[] AdditionalAircraftTypes { get; init; } = [];

    // TODO: Distance-based calculations.
    public required IReadOnlyDictionary<string, int> RunwayIntervals { get; init; }
}