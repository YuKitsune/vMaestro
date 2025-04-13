namespace Maestro.Core.Configuration;

public class ArrivalConfiguration
{
    public required string ArrivalIdentifier { get; init; }
    public required string FeederFix { get; init; }
    public required IReadOnlyDictionary<string, int> RunwayIntervals { get; init; }
}