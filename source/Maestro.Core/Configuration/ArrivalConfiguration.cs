using System.Text.RegularExpressions;
using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class ArrivalConfiguration
{
    public required string FeederFix { get; init; }
    public required Regex ArrivalRegex { get; init; }

    public required AircraftType AircraftType { get; init; }

    // TODO: Allow for distance-based calculations.
    public required IReadOnlyDictionary<string, int> RunwayIntervals { get; init; }
}