using Maestro.Core.Configuration;
using Maestro.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace Temp;

public static class YamlWriter
{
  public static string SaveToYaml(PluginConfigurationV2 config)
    {
        var serializerBuilder = new SerializerBuilder()
            .WithTypeConverter(new UriYamlConverter())
            .WithTypeConverter(new AircraftDescriptorYamlConverter())
            .WithTypeConverter(new AircraftDescriptorArrayYamlConverter())
            .WithTypeConverter(new DictionaryYamlConverter())
            .WithTypeConverter(new AirportConfigurationYamlConverter())
            .WithTypeConverter(new RunwayConfigurationYamlConverter())
            .WithTypeConverter(new TrajectoryConfigurationYamlConverter())
            .WithTypeConverter(new DepartureConfigurationYamlConverter())
            .WithTypeConverter(new LadderConfigurationYamlConverter())
            .WithTypeConverter(new LabelItemYamlConverter())
            .WithNamingConvention(NullNamingConvention.Instance);

        var serializer = serializerBuilder.Build();
        return serializer.Serialize(config);
    }
}

public class AircraftDescriptorYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(IAircraftDescriptor);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
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
            MultipleAircraftTypeDescriptor multiple => string.Join(",", multiple.TypeCodes),
            SpecificAircraftTypeDescriptor specific => specific.TypeCode,
            _ => throw new YamlException("Unexpected type when writing IAircraftDescriptor")
        };

        emitter.Emit(new Scalar(null, null, stringValue, ScalarStyle.Any, true, false));
    }
}

public class AircraftDescriptorArrayYamlConverter : IYamlTypeConverter
{
    private static readonly AircraftDescriptorYamlConverter _singleConverter = new();

    public bool Accepts(Type type)
    {
        return type == typeof(IAircraftDescriptor[]);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is not IAircraftDescriptor[] descriptors)
        {
            throw new YamlException("Expected IAircraftDescriptor[] but got different type");
        }

        emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));
        foreach (var descriptor in descriptors)
        {
            _singleConverter.WriteYaml(emitter, descriptor, typeof(IAircraftDescriptor));
        }
        emitter.Emit(new SequenceEnd());
    }
}

public class UriYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(Uri);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is Uri uri)
        {
            emitter.Emit(new Scalar(null, null, uri.ToString(), ScalarStyle.Any, true, false));
        }
        else
        {
            emitter.Emit(new Scalar(null, null, string.Empty, ScalarStyle.Any, true, false));
        }
    }
}


public class AirportConfigurationYamlConverter : IYamlTypeConverter
{

    public bool Accepts(Type type)
    {
        return type == typeof(AirportConfigurationV2);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is not AirportConfigurationV2 airport) return;

        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Block));

        emitter.Emit(new Scalar(null, null, "Identifier", ScalarStyle.Any, true, false));
        emitter.Emit(new Scalar(null, null, airport.Identifier, ScalarStyle.Any, true, false));

        emitter.Emit(new Scalar(null, null, "FeederFixes", ScalarStyle.Any, true, false));
        emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));
        foreach (var fix in airport.FeederFixes)
        {
            emitter.Emit(new Scalar(null, null, fix, ScalarStyle.Any, true, false));
        }
        emitter.Emit(new SequenceEnd());

        emitter.Emit(new Scalar(null, null, "Runways", ScalarStyle.Any, true, false));
        emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));
        foreach (var runway in airport.Runways)
        {
            emitter.Emit(new Scalar(null, null, runway, ScalarStyle.DoubleQuoted, false, false));
        }
        emitter.Emit(new SequenceEnd());

        // Let the default serializer handle the rest of the properties by emitting them manually
        // This is a workaround since we can't easily mix custom and default serialization

        if (airport.DefaultAircraftType != "B738")
        {
            emitter.Emit(new Scalar("DefaultAircraftType"));
            emitter.Emit(new Scalar(airport.DefaultAircraftType));
        }

        if (airport.DefaultPendingFlightState != State.Stable)
        {
            emitter.Emit(new Scalar("DefaultPendingFlightState"));
            emitter.Emit(new Scalar(airport.DefaultPendingFlightState.ToString()));
        }

        if (airport.DefaultDepartureFlightState != State.Unstable)
        {
            emitter.Emit(new Scalar("DefaultDepartureFlightState"));
            emitter.Emit(new Scalar(airport.DefaultDepartureFlightState.ToString()));
        }

        if (airport.DefaultDummyFlightState != State.Frozen)
        {
            emitter.Emit(new Scalar("DefaultDummyFlightState"));
            emitter.Emit(new Scalar(airport.DefaultDummyFlightState.ToString()));
        }

        if (airport.DefaultOffModeSeparationSeconds != 300)
        {
            emitter.Emit(new Scalar("DefaultOffModeSeparationSeconds"));
            emitter.Emit(new Scalar(airport.DefaultOffModeSeparationSeconds.ToString()));
        }

        if (airport.MinimumUnstableMinutes != 5)
        {
            emitter.Emit(new Scalar("MinimumUnstableMinutes"));
            emitter.Emit(new Scalar(airport.MinimumUnstableMinutes.ToString()));
        }

        if (airport.StabilityThresholdMinutes != 25)
        {
            emitter.Emit(new Scalar("StabilityThresholdMinutes"));
            emitter.Emit(new Scalar(airport.StabilityThresholdMinutes.ToString()));
        }

        if (airport.FrozenThresholdMinutes != 15)
        {
            emitter.Emit(new Scalar("FrozenThresholdMinutes"));
            emitter.Emit(new Scalar(airport.FrozenThresholdMinutes.ToString()));
        }

        if (airport.MaxLandedFlights != 5)
        {
            emitter.Emit(new Scalar("MaxLandedFlights"));
            emitter.Emit(new Scalar(airport.MaxLandedFlights.ToString()));
        }

        if (airport.LandedFlightTimeoutMinutes != 10)
        {
            emitter.Emit(new Scalar("LandedFlightTimeoutMinutes"));
            emitter.Emit(new Scalar(airport.LandedFlightTimeoutMinutes.ToString()));
        }

        if (airport.LostFlightTimeoutMinutes != 10)
        {
            emitter.Emit(new Scalar("LostFlightTimeoutMinutes"));
            emitter.Emit(new Scalar(airport.LostFlightTimeoutMinutes.ToString()));
        }

        // Use default serializer for complex nested objects
        var serializer = new SerializerBuilder()
            .WithTypeConverter(new RunwayConfigurationYamlConverter())
            .WithTypeConverter(new TrajectoryConfigurationYamlConverter())
            .WithTypeConverter(new DepartureConfigurationYamlConverter())
            .WithTypeConverter(new LadderConfigurationYamlConverter())
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

        emitter.Emit(new Scalar("RunwayModes"));
        var runwayModesYaml = serializer.Serialize(airport.RunwayModes);
        using var reader = new StringReader(runwayModesYaml);
        var parser = new YamlDotNet.Core.Parser(reader);
        parser.Consume<YamlDotNet.Core.Events.StreamStart>();
        parser.Consume<YamlDotNet.Core.Events.DocumentStart>();
        while (!parser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
        {
            var evt = parser.Consume<YamlDotNet.Core.Events.ParsingEvent>();
            emitter.Emit(evt);
        }

        emitter.Emit(new Scalar("Trajectories"));
        var trajectoriesYaml = serializer.Serialize(airport.Trajectories);
        using var trajReader = new StringReader(trajectoriesYaml);
        var trajParser = new YamlDotNet.Core.Parser(trajReader);
        trajParser.Consume<YamlDotNet.Core.Events.StreamStart>();
        trajParser.Consume<YamlDotNet.Core.Events.DocumentStart>();
        while (!trajParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
        {
            var evt = trajParser.Consume<YamlDotNet.Core.Events.ParsingEvent>();
            emitter.Emit(evt);
        }

        emitter.Emit(new Scalar("DepartureAirports"));
        var departuresYaml = serializer.Serialize(airport.DepartureAirports);
        using var depReader = new StringReader(departuresYaml);
        var depParser = new YamlDotNet.Core.Parser(depReader);
        depParser.Consume<YamlDotNet.Core.Events.StreamStart>();
        depParser.Consume<YamlDotNet.Core.Events.DocumentStart>();
        while (!depParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
        {
            var evt = depParser.Consume<YamlDotNet.Core.Events.ParsingEvent>();
            emitter.Emit(evt);
        }

        if (airport.Colours != null)
        {
            emitter.Emit(new Scalar("Colours"));
            var coloursYaml = serializer.Serialize(airport.Colours);
            using var colReader = new StringReader(coloursYaml);
            var colParser = new YamlDotNet.Core.Parser(colReader);
            colParser.Consume<YamlDotNet.Core.Events.StreamStart>();
            colParser.Consume<YamlDotNet.Core.Events.DocumentStart>();
            while (!colParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
            {
                var evt = colParser.Consume<YamlDotNet.Core.Events.ParsingEvent>();
                emitter.Emit(evt);
            }
        }

        emitter.Emit(new Scalar("Views"));
        var viewsYaml = serializer.Serialize(airport.Views);
        using var viewReader = new StringReader(viewsYaml);
        var viewParser = new YamlDotNet.Core.Parser(viewReader);
        viewParser.Consume<YamlDotNet.Core.Events.StreamStart>();
        viewParser.Consume<YamlDotNet.Core.Events.DocumentStart>();
        while (!viewParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
        {
            var evt = viewParser.Consume<YamlDotNet.Core.Events.ParsingEvent>();
            emitter.Emit(evt);
        }

        emitter.Emit(new Scalar("GlobalCoordinationMessages"));
        var globalMsgYaml = serializer.Serialize(airport.GlobalCoordinationMessages);
        using var globalReader = new StringReader(globalMsgYaml);
        var globalParser = new YamlDotNet.Core.Parser(globalReader);
        globalParser.Consume<YamlDotNet.Core.Events.StreamStart>();
        globalParser.Consume<YamlDotNet.Core.Events.DocumentStart>();
        while (!globalParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
        {
            var evt = globalParser.Consume<YamlDotNet.Core.Events.ParsingEvent>();
            emitter.Emit(evt);
        }

        emitter.Emit(new Scalar("FlightCoordinationMessages"));
        var flightMsgYaml = serializer.Serialize(airport.FlightCoordinationMessages);
        using var flightReader = new StringReader(flightMsgYaml);
        var flightParser = new YamlDotNet.Core.Parser(flightReader);
        flightParser.Consume<YamlDotNet.Core.Events.StreamStart>();
        flightParser.Consume<YamlDotNet.Core.Events.DocumentStart>();
        while (!flightParser.Accept<YamlDotNet.Core.Events.DocumentEnd>(out _))
        {
            var evt = flightParser.Consume<YamlDotNet.Core.Events.ParsingEvent>();
            emitter.Emit(evt);
        }

        emitter.Emit(new MappingEnd());
    }
}

public class DictionaryYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value == null) return;

        var dict = (System.Collections.IDictionary)value;
        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Block));

        foreach (System.Collections.DictionaryEntry entry in dict)
        {
            // Emit key
            emitter.Emit(new Scalar(entry.Key?.ToString() ?? string.Empty));

            // Emit value - check if it's an array
            if (entry.Value is Array array)
            {
                emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));
                foreach (var item in array)
                {
                    emitter.Emit(new Scalar(item?.ToString() ?? string.Empty));
                }
                emitter.Emit(new SequenceEnd());
            }
            else
            {
                // For other value types, emit as scalar
                emitter.Emit(new Scalar(entry.Value?.ToString() ?? string.Empty));
            }
        }

        emitter.Emit(new MappingEnd());
    }
}

public class RunwayConfigurationYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(RunwayConfigurationV2);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is not RunwayConfigurationV2 runway) return;

        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Flow));

        emitter.Emit(new Scalar("Identifier"));
        emitter.Emit(new Scalar(null, null, runway.Identifier, ScalarStyle.DoubleQuoted, false, false));

        if (!string.IsNullOrEmpty(runway.ApproachType))
        {
            emitter.Emit(new Scalar("ApproachType"));
            emitter.Emit(new Scalar(runway.ApproachType));
        }

        emitter.Emit(new Scalar("LandingRateSeconds"));
        emitter.Emit(new Scalar(runway.LandingRateSeconds.ToString()));

        if (runway.FeederFixes.Length > 0)
        {
            emitter.Emit(new Scalar("FeederFixes"));
            emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));
            foreach (var fix in runway.FeederFixes)
            {
                emitter.Emit(new Scalar(fix));
            }
            emitter.Emit(new SequenceEnd());
        }

        emitter.Emit(new MappingEnd());
    }
}

public class TrajectoryConfigurationYamlConverter : IYamlTypeConverter
{
    private static readonly AircraftDescriptorArrayYamlConverter _aircraftConverter = new();

    public bool Accepts(Type type)
    {
        return type == typeof(TrajectoryConfigurationV2);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is not TrajectoryConfigurationV2 traj) return;

        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Flow));

        emitter.Emit(new Scalar("FeederFix"));
        emitter.Emit(new Scalar(traj.FeederFix));

        emitter.Emit(new Scalar("Aircraft"));
        _aircraftConverter.WriteYaml(emitter, traj.Aircraft, typeof(IAircraftDescriptor[]));

        if (!string.IsNullOrEmpty(traj.ApproachType))
        {
            emitter.Emit(new Scalar("ApproachType"));
            emitter.Emit(new Scalar(traj.ApproachType));
        }

        if (!string.IsNullOrEmpty(traj.ApproachFix))
        {
            emitter.Emit(new Scalar("ApproachFix"));
            emitter.Emit(new Scalar(traj.ApproachFix));
        }

        emitter.Emit(new Scalar("RunwayIdentifier"));
        emitter.Emit(new Scalar(null, null, traj.RunwayIdentifier, ScalarStyle.DoubleQuoted, false, false));

        // Omit TrackMiles if it's 0 (unused)
        if (traj.TrackMiles != 0)
        {
            emitter.Emit(new Scalar("TrackMiles"));
            emitter.Emit(new Scalar(traj.TrackMiles.ToString()));
        }

        emitter.Emit(new Scalar("TimeToGoMinutes"));
        emitter.Emit(new Scalar(traj.TimeToGoMinutes.ToString()));

        // Omit PressureMinutes if it's 0 (default/unused)
        if (traj.PressureMinutes != 0)
        {
            emitter.Emit(new Scalar("PressureMinutes"));
            emitter.Emit(new Scalar(traj.PressureMinutes.ToString()));
        }

        // Omit MaxPressureMinutes if it's 0 (default/unused)
        if (traj.MaxPressureMinutes != 0)
        {
            emitter.Emit(new Scalar("MaxPressureMinutes"));
            emitter.Emit(new Scalar(traj.MaxPressureMinutes.ToString()));
        }

        emitter.Emit(new MappingEnd());
    }
}

public class DepartureConfigurationYamlConverter : IYamlTypeConverter
{
    private static readonly AircraftDescriptorArrayYamlConverter _aircraftConverter = new();

    public bool Accepts(Type type)
    {
        return type == typeof(DepartureConfigurationV2);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is not DepartureConfigurationV2 dep) return;

        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Flow));

        emitter.Emit(new Scalar("Identifier"));
        emitter.Emit(new Scalar(dep.Identifier));

        emitter.Emit(new Scalar("Aircraft"));
        _aircraftConverter.WriteYaml(emitter, dep.Aircraft, typeof(IAircraftDescriptor[]));

        emitter.Emit(new Scalar("Distance"));
        emitter.Emit(new Scalar(dep.Distance.ToString()));

        emitter.Emit(new Scalar("EstimatedFlightTimeMinutes"));
        emitter.Emit(new Scalar(dep.EstimatedFlightTimeMinutes.ToString()));

        emitter.Emit(new MappingEnd());
    }
}

public class LadderConfigurationYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(LadderConfigurationV2);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is not LadderConfigurationV2 ladder) return;

        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Flow));

        if (ladder.FeederFixes.Length > 0)
        {
            emitter.Emit(new Scalar("FeederFixes"));
            emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));
            foreach (var fix in ladder.FeederFixes)
            {
                emitter.Emit(new Scalar(fix));
            }
            emitter.Emit(new SequenceEnd());
        }

        if (ladder.Runways.Length > 0)
        {
            emitter.Emit(new Scalar("Runways"));
            emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));
            foreach (var rwy in ladder.Runways)
            {
                emitter.Emit(new Scalar(null, null, rwy, ScalarStyle.DoubleQuoted, false, false));
            }
            emitter.Emit(new SequenceEnd());
        }

        emitter.Emit(new MappingEnd());
    }
}

public class LabelItemYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return typeof(LabelItemConfigurationV2).IsAssignableFrom(type);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        throw new NotSupportedException("Reading is not supported in Temp project");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is not LabelItemConfigurationV2 item) return;

        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Flow));

        emitter.Emit(new Scalar("Type"));
        emitter.Emit(new Scalar(item.Type.ToString()));

        emitter.Emit(new Scalar("Width"));
        emitter.Emit(new Scalar(item.Width.ToString()));

        if (item.Padding != 1)
        {
            emitter.Emit(new Scalar("Padding"));
            emitter.Emit(new Scalar(item.Padding.ToString()));
        }

        if (item.ColourSources.Length > 0)
        {
            emitter.Emit(new Scalar("ColourSources"));
            emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Flow));
            foreach (var source in item.ColourSources)
            {
                emitter.Emit(new Scalar(source.ToString()));
            }
            emitter.Emit(new SequenceEnd());
        }

        // Handle type-specific properties
        switch (item)
        {
            case RequiredDelayItemConfigurationV2 req:
                emitter.Emit(new Scalar("ZeroDelaySymbol"));
                emitter.Emit(new Scalar(req.ZeroDelaySymbol));
                break;
            case ManualDelayItemConfigurationV2 manual:
                emitter.Emit(new Scalar("ZeroDelaySymbol"));
                emitter.Emit(new Scalar(manual.ZeroDelaySymbol));
                emitter.Emit(new Scalar("ManualDelaySymbol"));
                emitter.Emit(new Scalar(manual.ManualDelaySymbol));
                break;
            case ProfileSpeedItemConfigurationV2 speed:
                emitter.Emit(new Scalar("Symbol"));
                emitter.Emit(new Scalar(speed.Symbol));
                break;
            case CouplingStatusItemConfigurationV2 coupling:
                emitter.Emit(new Scalar("UncoupledSymbol"));
                emitter.Emit(new Scalar(coupling.UncoupledSymbol));
                break;
        }

        emitter.Emit(new MappingEnd());
    }
}
