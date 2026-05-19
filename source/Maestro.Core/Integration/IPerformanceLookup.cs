using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;

namespace Maestro.Core.Integration;

public interface IPerformanceLookup
{
    AircraftPerformanceData GetPerformanceDataFor(string aircraftType);

    /// <summary>
    ///     Returns the distance-to-go speed profile for the given aircraft.
    ///     Bands are ordered descending by <see cref="SpeedBand.ThresholdNM"/>.
    ///     The caller is responsible for splitting segments at band boundaries.
    /// </summary>
    SpeedBand[] GetSpeedProfile(AircraftPerformanceData aircraftPerformanceData);
}

public class AircraftPerformanceData(string typeCode, AircraftCategory aircraftCategory, WakeCategory? wakeCategory)
{
    public string TypeCode { get; } = typeCode;
    public AircraftCategory AircraftCategory { get; } = aircraftCategory;
    public WakeCategory? WakeCategory { get; } = wakeCategory;

    public static AircraftPerformanceData Default => new(
        "Unknown",
        AircraftCategory.Jet,
        null);
}
