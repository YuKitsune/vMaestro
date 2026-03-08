using Maestro.Core.Configuration;
using Maestro.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Maestro.Plugin.Configuration;

public static class YamlConfigurationLoader
{
    public static PluginConfiguration LoadFromYaml(string yamlContent)
    {
        var deserializerBuilder = new DeserializerBuilder()
            .WithTypeConverter(new ColorConfigurationTypeConverter())
            .WithTypeConverter(new AircraftDescriptorTypeConverter())
            .WithTypeConverter(new AircraftDescriptorArrayTypeConverter())
            .WithTypeConverter(new LabelItemTypeConverter())
            .WithNamingConvention(NullNamingConvention.Instance);

        var deserializer = deserializerBuilder.Build();
        var result = deserializer.Deserialize<PluginConfiguration>(yamlContent);

        return result;
    }
}

public class ColorConfigurationTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(Color);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        var value = scalar.Value;

        if (string.IsNullOrWhiteSpace(value))
            throw new YamlException("Color configuration cannot be empty");

        var parts = value.Split(',');
        if (parts.Length != 3)
            throw new YamlException($"Color configuration must be in 'R,G,B' format, got: {value}");

        if (!int.TryParse(parts[0].Trim(), out var red) || red < 0 || red > 255)
            throw new YamlException($"Invalid red value in color configuration: {parts[0]}");

        if (!int.TryParse(parts[1].Trim(), out var green) || green < 0 || green > 255)
            throw new YamlException($"Invalid green value in color configuration: {parts[1]}");

        if (!int.TryParse(parts[2].Trim(), out var blue) || blue < 0 || blue > 255)
            throw new YamlException($"Invalid blue value in color configuration: {parts[2]}");

        return new Color(red, green, blue);
    }


    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is not Color color)
            throw new YamlException("Expected ColorConfiguration but got different type");

        var stringValue = $"{color.Red},{color.Green},{color.Blue}";
        emitter.Emit(new Scalar(stringValue));
    }
}

public class AircraftDescriptorTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(IAircraftDescriptor);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        var value = scalar.Value;

        if (string.IsNullOrWhiteSpace(value))
            throw new YamlException("Aircraft descriptor cannot be empty");

        return value.ToUpper() switch
        {
            "ALL" => new AllAircraftTypesDescriptor(),
            "JET" => new AircraftCategoryDescriptor(AircraftCategory.Jet),
            "PROP" or "NONJET" => new AircraftCategoryDescriptor(AircraftCategory.NonJet),
            "LIGHT" or "L" => new WakeCategoryDescriptor(WakeCategory.Light),
            "MEDIUM" or "M" => new WakeCategoryDescriptor(WakeCategory.Medium),
            "HEAVY" or "H" => new WakeCategoryDescriptor(WakeCategory.Heavy),
            "SUPERHEAVY" or "SUPER" or "S" or "J" => new WakeCategoryDescriptor(WakeCategory.SuperHeavy),
            _ => new SpecificAircraftTypeDescriptor(value)
        };
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var stringValue = value switch
        {
            AllAircraftTypesDescriptor => "ALL",
            AircraftCategoryDescriptor { AircraftCategory: AircraftCategory.Jet } => "JET",
            AircraftCategoryDescriptor { AircraftCategory: AircraftCategory.NonJet } => "NONJET",
            WakeCategoryDescriptor { WakeCategory: WakeCategory.Light } => "LIGHT",
            WakeCategoryDescriptor { WakeCategory: WakeCategory.Medium } => "MEDIUM",
            WakeCategoryDescriptor { WakeCategory: WakeCategory.Heavy } => "HEAVY",
            WakeCategoryDescriptor { WakeCategory: WakeCategory.SuperHeavy } => "SUPER",
            SpecificAircraftTypeDescriptor specific => specific.TypeCode,
            _ => throw new YamlException("Unexpected type when writing IAircraftDescriptor")
        };

        emitter.Emit(new Scalar(stringValue));
    }
}

public class AircraftDescriptorArrayTypeConverter : IYamlTypeConverter
{
    static readonly AircraftDescriptorTypeConverter SingleConverter = new();

    public bool Accepts(Type type)
    {
        return type == typeof(IAircraftDescriptor[]);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var descriptors = new List<IAircraftDescriptor>();

        parser.Consume<SequenceStart>();
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            var descriptor = SingleConverter.ReadYaml(parser, typeof(IAircraftDescriptor), rootDeserializer);
            descriptors.Add((IAircraftDescriptor)descriptor);
        }

        return descriptors.ToArray();
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is not IAircraftDescriptor[] descriptors)
        {
            throw new YamlException("Expected IAircraftDescriptor[] but got different type");
        }

        emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Flow));
        foreach (var descriptor in descriptors)
        {
            SingleConverter.WriteYaml(emitter, descriptor, typeof(IAircraftDescriptor), serializer);
        }
        emitter.Emit(new SequenceEnd());
    }
}

public class LabelItemTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(LabelItemConfiguration) || type.IsSubclassOf(typeof(LabelItemConfiguration));
    }

    public object ReadYaml(IParser reader, Type type, ObjectDeserializer rootDeserializer)
    {
        if (type == typeof(LabelItemConfiguration) || type.IsSubclassOf(typeof(LabelItemConfiguration)))
        {
            reader.Consume<MappingStart>();

            string? typeValue = null;
            var properties = new Dictionary<string, object>();

            while (!reader.Accept<MappingEnd>(out _))
            {
                var key = reader.Consume<Scalar>();

                if (key.Value == "Type")
                {
                    var val = reader.Consume<Scalar>();
                    typeValue = val.Value;
                    properties[key.Value] = val.Value;
                }
                else if (key.Value == "ColourSources")
                {
                    // Handle array/sequence
                    var sources = new List<string>();
                    reader.Consume<SequenceStart>();
                    while (!reader.Accept<SequenceEnd>(out _))
                    {
                        var item = reader.Consume<Scalar>();
                        sources.Add(item.Value);
                    }
                    reader.Consume<SequenceEnd>();
                    properties[key.Value] = sources;
                }
                else
                {
                    // Handle scalar values
                    var val = reader.Consume<Scalar>();
                    properties[key.Value] = val.Value;
                }
            }

            reader.Consume<MappingEnd>();

            if (string.IsNullOrEmpty(typeValue))
            {
                throw new YamlException("Label item missing 'Type' field");
            }

            var concreteType = GetLabelItemType(typeValue!);
            if (concreteType == null)
            {
                throw new YamlException($"Unknown label item type: {typeValue}");
            }

            return DeserializeLabelItem(concreteType, properties);
        }

        throw new YamlException($"Unexpected type for LabelItemTypeConverter: {type}");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        throw new NotImplementedException("Writing YAML is not supported");
    }

    static Type? GetLabelItemType(string typeValue)
    {
        return typeValue switch
        {
            "Callsign" => typeof(CallsignItemConfiguration),
            "AircraftType" => typeof(AircraftTypeItemConfiguration),
            "AircraftWakeCategory" => typeof(AircraftWakeCategoryItemConfiguration),
            "Runway" => typeof(RunwayItemConfiguration),
            "ApproachType" => typeof(ApproachTypeItemConfiguration),
            "LandingTime" => typeof(LandingTimeItemConfiguration),
            "FeederFixTime" => typeof(FeederFixTimeItemConfiguration),
            "RequiredDelay" => typeof(RequiredDelayItemConfiguration),
            "RemainingDelay" => typeof(RemainingDelayItemConfiguration),
            "ManualDelay" => typeof(ManualDelayItemConfiguration),
            "ProfileSpeed" => typeof(ProfileSpeedItemConfiguration),
            "CouplingStatus" => typeof(CouplingStatusItemConfiguration),
            _ => null
        };
    }

    static object DeserializeLabelItem(Type concreteType, Dictionary<string, object> properties)
    {
        var width = int.Parse((string)properties["Width"]);
        var padding = properties.ContainsKey("Padding") ? int.Parse((string)properties["Padding"]) : 1;
        var colourSources = properties.ContainsKey("ColourSources")
            ? ParseColourSources((List<string>)properties["ColourSources"])
            : [];

        return concreteType.Name switch
        {
            nameof(CallsignItemConfiguration) => new CallsignItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(AircraftTypeItemConfiguration) => new AircraftTypeItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(AircraftWakeCategoryItemConfiguration) => new AircraftWakeCategoryItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(RunwayItemConfiguration) => new RunwayItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(ApproachTypeItemConfiguration) => new ApproachTypeItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(LandingTimeItemConfiguration) => new LandingTimeItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(FeederFixTimeItemConfiguration) => new FeederFixTimeItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(RequiredDelayItemConfiguration) => new RequiredDelayItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(RemainingDelayItemConfiguration) => new RemainingDelayItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(ManualDelayItemConfiguration) => new ManualDelayItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources,
                ZeroDelaySymbol = (string)properties["ZeroDelaySymbol"],
                ManualDelaySymbol = (string)properties["ManualDelaySymbol"]
            },
            nameof(ProfileSpeedItemConfiguration) => new ProfileSpeedItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources,
                Symbol = (string)properties["Symbol"]
            },
            nameof(CouplingStatusItemConfiguration) => new CouplingStatusItemConfiguration
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources,
                UncoupledSymbol = (string)properties["UncoupledSymbol"]
            },
            _ => throw new YamlException($"Unsupported label item type: {concreteType.Name}")
        };
    }

    static LabelItemColourSource[] ParseColourSources(List<string> sources)
    {
        if (sources.Count == 0)
            return [];

        return sources
            .Select(s => (LabelItemColourSource)Enum.Parse(typeof(LabelItemColourSource), s.Trim()))
            .ToArray();
    }
}

