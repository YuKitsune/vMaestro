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


    public int DefaultOffModeSeparationSeconds { get; init; } = 300;

    public required RunwayModeConfiguration[] RunwayModes { get; init; }
    public required ArrivalConfiguration[] Arrivals { get; init; }
    public required ViewConfiguration[] Views { get; init; }
    public required DepartureAirportConfiguration[] DepartureAirports { get; init; } = [];
    // TODO: Average taxi times and terminal assignments
}

public class DepartureAirportConfiguration
{
    public required string Identifier { get; init; }
    public required double Distance { get; init; }
    public DepartureAirportFlightTimeConfiguration[] FlightTimes { get; init; } = [];
}

public class DepartureAirportFlightTimeConfiguration
{
    public required IAircraftDescriptor Aircraft { get; init; }
    public required TimeSpan AverageFlightTime { get; init; }
}

[JsonConverter(typeof(AircraftDescriptorJsonConverter))]
public interface IAircraftDescriptor;

[DebuggerDisplay("All")]
public record AllAircraftTypesDescriptor : IAircraftDescriptor;

[DebuggerDisplay("{TypeCode}")]
public record SpecificAircraftTypeDescriptor(string TypeCode) : IAircraftDescriptor;

[DebuggerDisplay("{DisplayValues}")]
public record MultipleAircraftTypeDescriptor(string[] TypeCodes) : IAircraftDescriptor
{
    string DisplayValues => string.Join(", ", TypeCodes);
}

[DebuggerDisplay("{AircraftCategory}")]
public record AircraftCategoryDescriptor(AircraftCategory AircraftCategory) : IAircraftDescriptor;

[DebuggerDisplay("{WakeCategory}")]
public record WakeCategoryDescriptor(WakeCategory WakeCategory) : IAircraftDescriptor;

public class AircraftDescriptorJsonConverter : JsonConverter<IAircraftDescriptor>
{
    public override void WriteJson(JsonWriter writer, IAircraftDescriptor? value, JsonSerializer serializer)
    {
        switch (value)
        {
            case AllAircraftTypesDescriptor:
                writer.WriteValue("ALL");
                break;

            case AircraftCategoryDescriptor { AircraftCategory: AircraftCategory.Jet }:
                writer.WriteValue("JET");
                break;

            case AircraftCategoryDescriptor { AircraftCategory: AircraftCategory.NonJet }:
                writer.WriteValue("NONJET");
                break;

            case WakeCategoryDescriptor { WakeCategory: WakeCategory.Light }:
                writer.WriteValue("LIGHT");
                break;

            case WakeCategoryDescriptor { WakeCategory: WakeCategory.Medium }:
                writer.WriteValue("MEDIUM");
                break;

            case WakeCategoryDescriptor { WakeCategory: WakeCategory.Heavy }:
                writer.WriteValue("HEAVY");
                break;

            case WakeCategoryDescriptor { WakeCategory: WakeCategory.SuperHeavy }:
                writer.WriteValue("SUPER");
                break;

            case MultipleAircraftTypeDescriptor multipleAircraftTypeDescriptor:
                writer.WriteValue(string.Join(",", multipleAircraftTypeDescriptor.TypeCodes));
                break;

            case SpecificAircraftTypeDescriptor specificAircraftTypeConfiguration:
                writer.WriteValue(specificAircraftTypeConfiguration.TypeCode);
                break;

            default:
                throw new JsonSerializationException("Unexpected type when writing IAircraftDescriptor.");
        }
    }

    public override IAircraftDescriptor ReadJson(
        JsonReader reader,
        Type objectType,
        IAircraftDescriptor? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        var value = reader.Value;
        if (value is not string valueStr)
            throw new JsonSerializationException("Unexpected value when reading IAircraftDescriptor.");

        return valueStr.ToUpper() switch
        {
            "ALL" => new AllAircraftTypesDescriptor(),
            "JET" => new AircraftCategoryDescriptor(AircraftCategory.Jet),
            "PROP" or "NONJET" => new AircraftCategoryDescriptor(AircraftCategory.NonJet),
            "LIGHT" or "L" => new WakeCategoryDescriptor(WakeCategory.Light),
            "MEDIUM" or "M" => new WakeCategoryDescriptor(WakeCategory.Medium),
            "HEAVY" or "H" => new WakeCategoryDescriptor(WakeCategory.Heavy),
            "SUPERHEAVY" or "SUPER" or "S" or "J" => new WakeCategoryDescriptor(WakeCategory.SuperHeavy),
            _ => valueStr.Contains(",")
                ? new MultipleAircraftTypeDescriptor(valueStr.Split(','))
                : new SpecificAircraftTypeDescriptor(valueStr)
        };
    }
}
