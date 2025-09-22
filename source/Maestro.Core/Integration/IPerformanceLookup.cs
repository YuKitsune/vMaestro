using Maestro.Core.Model;

namespace Maestro.Core.Integration;

public interface IPerformanceLookup
{
    AircraftPerformanceData GetPerformanceDataFor(string aircraftType);
}

public class AircraftPerformanceData
{
    public required string TypeCode { get; init; }
    public required WakeCategory WakeCategory { get; init; }

    public required AircraftCategory AircraftCategory { get; init; }

    public static AircraftPerformanceData Default => new()
    {
        TypeCode = "Unknown",
        WakeCategory = WakeCategory.Medium,
        AircraftCategory = AircraftCategory.Jet
    };
}
