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
            .WithTypeConverter(new AircraftDescriptorTypeConverter())
            .WithTypeConverter(new AircraftDescriptorArrayTypeConverter())
            .WithNodeDeserializer(
                inner => new LabelItemNodeDeserializer(inner),
                s => s.InsteadOf<ObjectNodeDeserializer>())
            .WithNamingConvention(NullNamingConvention.Instance);

        var deserializer = deserializerBuilder.Build();
        return deserializer.Deserialize<PluginConfigurationV2>(yamlContent);
    }
}

public class AircraftDescriptorTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(IAircraftDescriptor);
    }

    public object? ReadYaml(IParser parser, Type type)
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

    public void WriteYaml(IEmitter emitter, object? value, Type type)
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

    public object? ReadYaml(IParser parser, Type type)
    {
        var descriptors = new List<IAircraftDescriptor>();

        parser.Consume<SequenceStart>();
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            var descriptor = _singleConverter.ReadYaml(parser, typeof(IAircraftDescriptor));
            if (descriptor != null)
            {
                descriptors.Add((IAircraftDescriptor)descriptor);
            }
        }

        return descriptors.ToArray();
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is not IAircraftDescriptor[] descriptors)
        {
            throw new YamlException("Expected IAircraftDescriptor[] but got different type");
        }

        emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Flow));
        foreach (var descriptor in descriptors)
        {
            _singleConverter.WriteYaml(emitter, descriptor, typeof(IAircraftDescriptor));
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

    public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
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

        return _inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value);
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
            : new[] { LabelItemColourSource.State };

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
        if (sources == null || sources.Count == 0)
            return new[] { LabelItemColourSource.State };

        return sources
            .Select(s => (LabelItemColourSource)Enum.Parse(typeof(LabelItemColourSource), s.Trim()))
            .ToArray();
    }
}

