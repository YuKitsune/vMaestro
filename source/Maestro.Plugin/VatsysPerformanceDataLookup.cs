using Maestro.Core.Model;
using vatsys;

namespace Maestro.Plugin;

public class VatsysPerformanceDataLookup : IPerformanceLookup
{
    public AircraftPerformanceData? GetPerformanceDataFor(string aircraftType)
    {
        var performanceData = Performance.GetPerformanceData(aircraftType);
        if (performanceData is null)
            return null;
        
        var typeAndWake = Performance.GetAircraftFromType(aircraftType);
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
            WakeCategory = wakeCategory,
            IsJet = performanceData.IsJet
        };
    }
}