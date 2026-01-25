using System.Diagnostics;
using Maestro.Core.Model;
using Newtonsoft.Json;

namespace Maestro.Core.Configuration;

public class AirportConfiguration
{
    public required string Identifier { get; init; }
    public required string[] FeederFixes { get; init; }

    /// <summary>
    ///     The default aircraft type code to use when no type code was provided when inserting a flight manually
    ///     into the sequence.
    /// </summary>
    public string DefaultInsertedFlightAircraftType { get; init; } = "B738";

    /// <summary>
    ///     The initial state assigned to a flight that has been inserted manually by the controller.
    /// </summary>
    public State ManuallyInsertedFlightState { get; init; } = State.Stable;

    /// <summary>
    ///     The initial state assigned to a flight departing from a configured departure airport.
    /// </summary>
    public State InitialDepartureFlightState { get; init; } = State.Unstable;

    /// <summary>
    ///     The initial state assigned to a dummy flight.
    /// </summary>
    public State DummyFlightState { get; init; } = State.Frozen;

    /// <summary>
    ///     The number of minutes before landed flights are removed from the sequence.
    /// </summary>
    public int LandedFlightTimeoutMinutes { get; init; } = 10;

    /// <summary>
    ///     The maximum number of Landed flights which can remain in the sequence in the event of an overshoot.
    /// </summary>
    public int MaxLandedFlights { get; init; } = 5;
    public required RunwayConfiguration[] Runways { get; init; }
    public required RunwayModeConfiguration[] RunwayModes { get; init; }
    public required ArrivalConfiguration[] Arrivals { get; init; }
    public required ViewConfiguration[] Views { get; init; }
    public required DepartureAirportConfiguration[] DepartureAirports { get; init; } = [];

    // TODO: Default TTG
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
