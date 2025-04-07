namespace Maestro.Core.Model;

public interface IPerformanceLookup
{
    AircraftPerformanceData GetPerformanceDataFor(string aircraftType);
}

public class PerformanceLookup : IPerformanceLookup
{
    // TODO: Source from vatSys performance

    public AircraftPerformanceData GetPerformanceDataFor(string aircraftType)
    {
        return new AircraftPerformanceData();
    }
}

public class AircraftPerformanceData
{
    public WakeCategory WakeCategory { get; init; }
        
    public bool IsJet { get; set; }

    public int GetDescentSpeedAt(int altitude) => 280; // TODO:
}

public enum WakeCategory
{
    Light,
    Medium,
    Heavy,
    SuperHeavy
}