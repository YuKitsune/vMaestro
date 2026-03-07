using Maestro.Core.Configuration;
using Maestro.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Maestro.Plugin.Configuration;

public static class YamlConfigurationLoader
{
    public static PluginConfigurationV2 LoadFromYaml(string yamlContent)
    {
        var deserializerBuilder = new DeserializerBuilder()
            .WithTypeConverter(new ColorConfigurationTypeConverter())
            .WithTypeConverter(new AircraftDescriptorTypeConverter())
            .WithTypeConverter(new AircraftDescriptorArrayTypeConverter())
            .WithNodeDeserializer(
                inner => new ColorConfigurationDictionaryNodeDeserializer(inner),
                s => s.InsteadOf<DictionaryNodeDeserializer>())
            .WithNodeDeserializer(
                inner => new LabelItemNodeDeserializer(inner),
                s => s.InsteadOf<ObjectNodeDeserializer>())
            .WithNamingConvention(NullNamingConvention.Instance);

        var deserializer = deserializerBuilder.Build();
        return deserializer.Deserialize<PluginConfigurationV2>(yamlContent);
    }
}

public class ColorConfigurationDictionaryNodeDeserializer : INodeDeserializer
{
    private readonly INodeDeserializer _inner;
    private static readonly ColorConfigurationTypeConverter _colorConverter = new();

    public ColorConfigurationDictionaryNodeDeserializer(INodeDeserializer inner)
    {
        _inner = inner;
    }

    public bool Deserialize(
        IParser reader,
        Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value,
        ObjectDeserializer rootDeserializer)
    {
        // Check if this is a dictionary with ColorConfiguration values
        if (expectedType.IsGenericType &&
            expectedType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
            expectedType.GetGenericArguments()[1] == typeof(ColorConfiguration))
        {
            if (!reader.Accept<MappingStart>(out _))
            {
                value = null;
                return false;
            }

            reader.Consume<MappingStart>();

            var keyType = expectedType.GetGenericArguments()[0];
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, typeof(ColorConfiguration));
            var dictionary = Activator.CreateInstance(dictionaryType);
            var addMethod = dictionaryType.GetMethod("Add");

            while (!reader.Accept<MappingEnd>(out _))
            {
                var keyScalar = reader.Consume<Scalar>();

                // Use the color converter to deserialize the value
                var colorValue = _colorConverter.ReadYaml(reader, typeof(ColorConfiguration), rootDeserializer);

                if (colorValue != null)
                {
                    object? key;
                    if (keyType == typeof(string))
                    {
                        key = keyScalar.Value;
                    }
                    else if (keyType.IsEnum)
                    {
                        try
                        {
                            key = Enum.Parse(keyType, keyScalar.Value);
                        }
                        catch
                        {
                            continue; // Skip invalid enum values
                        }
                    }
                    else
                    {
                        continue; // Unsupported key type
                    }

                    addMethod?.Invoke(dictionary, new[] { key, colorValue });
                }
            }

            reader.Consume<MappingEnd>();
            value = dictionary;
            return true;
        }

        return _inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer);
    }
}

public class ColorConfigurationTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(ColorConfiguration);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
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

        return new ColorConfiguration(red, green, blue);
    }


    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is not ColorConfiguration color)
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

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
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
    private static readonly AircraftDescriptorTypeConverter _singleConverter = new();

    public bool Accepts(Type type)
    {
        return type == typeof(IAircraftDescriptor[]);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var descriptors = new List<IAircraftDescriptor>();

        parser.Consume<SequenceStart>();
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            var descriptor = _singleConverter.ReadYaml(parser, typeof(IAircraftDescriptor), rootDeserializer);
            if (descriptor != null)
            {
                descriptors.Add((IAircraftDescriptor)descriptor);
            }
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
            _singleConverter.WriteYaml(emitter, descriptor, typeof(IAircraftDescriptor), serializer);
        }
        emitter.Emit(new SequenceEnd());
    }
}

public class LabelItemNodeDeserializer : INodeDeserializer
{
    private readonly INodeDeserializer _inner;

    public LabelItemNodeDeserializer(INodeDeserializer inner)
    {
        _inner = inner;
    }

    public bool Deserialize(
        IParser reader,
        Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value,
        ObjectDeserializer rootDeserializer)
    {
        if (expectedType == typeof(LabelItemConfigurationV2) || expectedType.IsSubclassOf(typeof(LabelItemConfigurationV2)))
        {
            if (!reader.Accept<MappingStart>(out _))
            {
                value = null;
                return false;
            }

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

            value = DeserializeLabelItem(concreteType, properties);
            return true;
        }

        return _inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer);
    }

    private static Type? GetLabelItemType(string typeValue)
    {
        return typeValue switch
        {
            "Callsign" => typeof(CallsignItemConfigurationV2),
            "AircraftType" => typeof(AircraftTypeItemConfigurationV2),
            "AircraftWakeCategory" => typeof(AircraftWakeCategoryItemConfigurationV2),
            "Runway" => typeof(RunwayItemConfigurationV2),
            "ApproachType" => typeof(ApproachTypeItemConfigurationV2),
            "LandingTime" => typeof(LandingTimeItemConfigurationV2),
            "FeederFixTime" => typeof(FeederFixTimeItemConfigurationV2),
            "RequiredDelay" => typeof(RequiredDelayItemConfigurationV2),
            "RemainingDelay" => typeof(RemainingDelayItemConfigurationV2),
            "ManualDelay" => typeof(ManualDelayItemConfigurationV2),
            "ProfileSpeed" => typeof(ProfileSpeedItemConfigurationV2),
            "CouplingStatus" => typeof(CouplingStatusItemConfigurationV2),
            _ => null
        };
    }

    private static object DeserializeLabelItem(Type concreteType, Dictionary<string, object> properties)
    {
        var width = int.Parse((string)properties["Width"]);
        var padding = properties.ContainsKey("Padding") ? int.Parse((string)properties["Padding"]) : 1;
        var colourSources = properties.ContainsKey("ColourSources")
            ? ParseColourSources((List<string>)properties["ColourSources"])
            : [];

        return concreteType.Name switch
        {
            nameof(CallsignItemConfigurationV2) => new CallsignItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(AircraftTypeItemConfigurationV2) => new AircraftTypeItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(AircraftWakeCategoryItemConfigurationV2) => new AircraftWakeCategoryItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(RunwayItemConfigurationV2) => new RunwayItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(ApproachTypeItemConfigurationV2) => new ApproachTypeItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(LandingTimeItemConfigurationV2) => new LandingTimeItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(FeederFixTimeItemConfigurationV2) => new FeederFixTimeItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(RequiredDelayItemConfigurationV2) => new RequiredDelayItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(RemainingDelayItemConfigurationV2) => new RemainingDelayItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources
            },
            nameof(ManualDelayItemConfigurationV2) => new ManualDelayItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources,
                ZeroDelaySymbol = (string)properties["ZeroDelaySymbol"],
                ManualDelaySymbol = (string)properties["ManualDelaySymbol"]
            },
            nameof(ProfileSpeedItemConfigurationV2) => new ProfileSpeedItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources,
                Symbol = (string)properties["Symbol"]
            },
            nameof(CouplingStatusItemConfigurationV2) => new CouplingStatusItemConfigurationV2
            {
                Width = width,
                Padding = padding,
                ColourSources = colourSources,
                UncoupledSymbol = (string)properties["UncoupledSymbol"]
            },
            _ => throw new YamlException($"Unsupported label item type: {concreteType.Name}")
        };
    }

    private static LabelItemColourSource[] ParseColourSources(List<string> sources)
    {
        if (sources.Count == 0)
            return [];

        return sources
            .Select(s => (LabelItemColourSource)Enum.Parse(typeof(LabelItemColourSource), s.Trim()))
            .ToArray();
    }
}

