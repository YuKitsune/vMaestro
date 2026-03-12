using System.Diagnostics;
using Maestro.Core.Model;
using Newtonsoft.Json;

namespace Maestro.Core.Configuration;

[JsonConverter(typeof(AircraftDescriptorJsonConverter))]
public interface IAircraftDescriptor;

[DebuggerDisplay("All")]
public record AllAircraftTypesDescriptor : IAircraftDescriptor;

[DebuggerDisplay("{TypeCode}")]
public record SpecificAircraftTypeDescriptor(string TypeCode) : IAircraftDescriptor;

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
            _ => new SpecificAircraftTypeDescriptor(valueStr)
        };
    }
}
