using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;

namespace Maestro.Core.Integration;

public class YamlPerformanceLookup(AircraftPerformanceConfiguration[] performanceConfigurations) : IPerformanceLookup
{
    public AircraftPerformanceData GetPerformanceDataFor(string aircraftType)
    {
        var entry = Find(aircraftType);
        if (entry is null)
            return AircraftPerformanceData.Default;

        return new AircraftPerformanceData(
            entry.TypeCode,
            entry.IsJet ? AircraftCategory.Jet : AircraftCategory.NonJet,
            entry.WakeCategory);
    }

    public int? GetApproachSpeed(string aircraftType)
    {
        return Find(aircraftType)?.DescentSpeedKnots;
    }

    AircraftPerformanceConfiguration? Find(string aircraftType) =>
        performanceConfigurations.FirstOrDefault(x =>
            string.Equals(x.TypeCode, aircraftType, StringComparison.OrdinalIgnoreCase));
}
