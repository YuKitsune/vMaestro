using Maestro.Core.Integration;
using Maestro.Core.Model;
using vatsys;

namespace Maestro.Plugin;

public class VatsysPerformanceDataLookup : IPerformanceLookup
{
    public AircraftPerformanceData GetPerformanceDataFor(string aircraftType)
    {
        var performanceData = Performance.GetPerformanceData(aircraftType);
        if (performanceData is null)
            return AircraftPerformanceData.Default;

        var typeAndWake = Performance.GetAircraftFromType(aircraftType);
        if (typeAndWake is null)
            return AircraftPerformanceData.Default;

        var wakeCategory = typeAndWake.WakeCategory switch
        {
            "L" => WakeCategory.Light,
            "M" => WakeCategory.Medium,
            "H" => WakeCategory.Heavy,
            "J" => WakeCategory.SuperHeavy,
            _ => WakeCategory.Heavy
        };

        return new AircraftPerformanceData
        {
            TypeCode = aircraftType,
            WakeCategory = wakeCategory,
            AircraftCategory = performanceData.IsJet
                ? AircraftCategory.Jet
                : AircraftCategory.NonJet
        };
    }
}
