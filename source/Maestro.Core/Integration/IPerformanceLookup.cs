using Maestro.Core.Model;

namespace Maestro.Core.Integration;

public interface IPerformanceLookup
{
    AircraftPerformanceData GetPerformanceDataFor(string aircraftType);
}

public class AircraftPerformanceData(string typeCode, AircraftCategory aircraftCategory, WakeCategory wakeCategory)
{
    public string TypeCode { get; } = typeCode;
    public AircraftCategory AircraftCategory { get; } =  aircraftCategory;
    public WakeCategory WakeCategory { get; } = wakeCategory;

    public static AircraftPerformanceData Default => new(
        "Unknown",
        AircraftCategory.Jet,
        WakeCategory.Medium);
}
