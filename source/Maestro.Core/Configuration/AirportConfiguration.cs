using System.Diagnostics;
using Maestro.Core.Model;
using Newtonsoft.Json;

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
    public required double Distance { get; init; }
    public DepartureAirportFlightTimeConfiguration[] FlightTimes { get; init; } = [];
}

public class DepartureAirportFlightTimeConfiguration
{
    public required IAircraftTypeConfiguration AircraftType { get; init; }
    public required TimeSpan AverageFlightTime { get; init; }
}

[JsonConverter(typeof(AircraftTypeConfigurationJsonConverter))]
public interface IAircraftTypeConfiguration;

[DebuggerDisplay("All")]
public record AllAircraftTypesConfiguration : IAircraftTypeConfiguration;

[DebuggerDisplay("{TypeCode}")]
public record SpecificAircraftTypeConfiguration(string TypeCode) : IAircraftTypeConfiguration;

[DebuggerDisplay("{Category}")]
public record AircraftCategoryConfiguration(AircraftCategory Category) : IAircraftTypeConfiguration;

public class AircraftTypeConfigurationJsonConverter : JsonConverter<IAircraftTypeConfiguration>
{
    public override void WriteJson(JsonWriter writer, IAircraftTypeConfiguration? value, JsonSerializer serializer)
    {
        switch (value)
        {
            case AllAircraftTypesConfiguration:
                writer.WriteValue("ALL");
                break;

            case AircraftCategoryConfiguration { Category: AircraftCategory.Jet }:
                writer.WriteValue("JET");
                break;

            case AircraftCategoryConfiguration { Category: AircraftCategory.NonJet }:
                writer.WriteValue("NONJET");
                break;

            case SpecificAircraftTypeConfiguration specificAircraftTypeConfiguration:
                writer.WriteValue(specificAircraftTypeConfiguration.TypeCode);
                break;

            default:
                throw new JsonSerializationException("Unexpected type when writing IAircraftTypeConfiguration.");
        }
    }

    public override IAircraftTypeConfiguration ReadJson(
        JsonReader reader,
        Type objectType,
        IAircraftTypeConfiguration? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        var value = reader.Value;
        if (value is not string valueStr)
            throw new JsonSerializationException("Unexpected value when reading IAircraftTypeConfiguration.");

        return valueStr.ToUpper() switch
        {
            "ALL" => new AllAircraftTypesConfiguration(),
            "JET" => new AircraftCategoryConfiguration(AircraftCategory.Jet),
            "PROP" or "NONJET" => new AircraftCategoryConfiguration(AircraftCategory.NonJet),
            _ => new SpecificAircraftTypeConfiguration(valueStr),
        };
    }
}
