using Maestro.Core.Dtos;

namespace Maestro.Core.Model;

public class FixEstimate(string fixIdentifier, Coordinate coordinate, DateTimeOffset estimate)
{
    public string FixIdentifier { get; } = fixIdentifier;
    public Coordinate Coordinate { get; } = coordinate;
    public DateTimeOffset Estimate { get; } = estimate;
}