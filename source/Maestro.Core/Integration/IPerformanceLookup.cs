using Maestro.Contracts.Shared;

namespace Maestro.Core.Integration;

public interface IPerformanceLookup
{
    AircraftPerformanceData GetPerformanceDataFor(string aircraftType);

    /// <summary>
    ///     Returns the approach speed in knots (TAS) for the given aircraft type,
    ///     or null if no speed data is available.
    /// </summary>
    int? GetApproachSpeed(string aircraftType);
}

public class AircraftPerformanceData(string typeCode, AircraftCategory aircraftCategory, WakeCategory wakeCategory)
{
    public string TypeCode { get; } = typeCode;
    public AircraftCategory AircraftCategory { get; } = aircraftCategory;
    public WakeCategory WakeCategory { get; } = wakeCategory;

    public static AircraftPerformanceData Default => new(
        "Unknown",
        AircraftCategory.Jet,
        WakeCategory.Medium);
}
