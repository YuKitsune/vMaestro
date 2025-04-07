using Maestro.Core.Model;

namespace Maestro.Core.Tests;

public class FixedPerformanceLookup : IPerformanceLookup
{
    public AircraftPerformanceData GetPerformanceDataFor(string aircraftType)
    {
        return new AircraftPerformanceData
        {
            IsJet = true,
            WakeCategory = WakeCategory.Medium
        };
    }
}