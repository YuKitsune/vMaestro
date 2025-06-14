using Maestro.Core.Configuration;
using Maestro.Core.Model;
using vatsys;

namespace Maestro.Plugin;

public class VatsysPerformanceDataLookup(IMaestroConfiguration maestroConfiguration) : IPerformanceLookup
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

        var type = performanceData.IsJet
            ? AircraftType.Jet
            : AircraftType.NonJet;
        var reclassification = maestroConfiguration.Reclassifications.FirstOrDefault(r => r.AircraftType == aircraftType);
        if (reclassification is not null)
        {
            type = reclassification.NewClassification;
        }

        return new AircraftPerformanceData
        {
            WakeCategory = wakeCategory,
            Type = type
        };
    }
}