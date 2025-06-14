namespace Maestro.Core.Model;

public interface IPerformanceLookup
{
    AircraftPerformanceData? GetPerformanceDataFor(string aircraftType);
}

public class AircraftPerformanceData
{
    public WakeCategory WakeCategory { get; init; }
        
    public AircraftType Type { get; init; }
}

public enum WakeCategory
{
    Light,
    Medium,
    Heavy,
    SuperHeavy
}