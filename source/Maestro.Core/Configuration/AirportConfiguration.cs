using System.Diagnostics;
using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class AirportConfiguration
{
    public required string Identifier { get; init; }
    public required string[] FeederFixes { get; init; }
    public required Dictionary<string, string[]> PreferredRunways { get; init; } = new();
    public required RunwayConfiguration[] Runways { get; init; }
    public required RunwayModeConfiguration[] RunwayModes { get; init; }
    public required ArrivalConfiguration[] Arrivals { get; init; }
    public required ViewConfiguration[] Views { get; init; }
    public required DepartureAirportConfiguration[] DepartureAirports { get; init; } = [];
}

public class DepartureAirportConfiguration
{
    public required string Identifier { get; init; }
    public DepartureAirportFlightTimeConfiguration[] FlightTimes { get; init; } = [];
}

public class DepartureAirportFlightTimeConfiguration
{
    public required IAircraftTypeConfiguration AircraftType { get; init; }
    public required TimeSpan AverageFlightTime { get; init; }
}

public interface IAircraftTypeConfiguration;

[DebuggerDisplay("All")]
public record AllAircraftTypesConfiguration : IAircraftTypeConfiguration;

[DebuggerDisplay("{TypeCode}")]
public record SpecificAircraftTypeConfiguration(string TypeCode) : IAircraftTypeConfiguration;

[DebuggerDisplay("{Category}")]
public record AircraftCategoryConfiguration(AircraftCategory Category) : IAircraftTypeConfiguration;

