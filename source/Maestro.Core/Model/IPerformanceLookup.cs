namespace Maestro.Core.Model;

public interface IPerformanceLookup
{
    AircraftPerformanceData? GetPerformanceDataFor(string aircraftType);
}

public class AircraftPerformanceData
{
    public required string Type { get; init; }
    public required WakeCategory WakeCategory { get; init; }
        
    public required AircraftCategory AircraftCategory { get; init; }
}

public enum WakeCategory
{
    Light,
    Medium,
    Heavy,
    SuperHeavy
}

public enum AircraftCategory
{
    Jet,
    NonJet
}