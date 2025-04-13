using Maestro.Core.Model;

namespace Maestro.Core.Dtos.Configuration;

public class SeparationRule
{
    // public required string Name { get; init; }
    public required TimeSpan Interval { get; init; }
    public required WakeCategory WakeCategoryLeader { get; init; }
    public required WakeCategory WakeCategoryFollower { get; init; }
    public required string[] AircraftTypeCodesLeader { get; init; }
    public required string[] AircraftTypeCodesFollower { get; init; }
}