using System.CommandLine;
using System.Globalization;
using System.Xml.Linq;
using Maestro.Tools;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace Maestro.Tools.Commands;

public static class ExtractStarsCommand
{
    public static Command Build()
    {
        var airspaceOption = new Option<FileInfo>("--airspace", "Path to vatSys Airspace.xml") { IsRequired = true };
        var configOption = new Option<FileInfo>("--config", "Path to maestro-tools.yaml config file") { IsRequired = true };

        var command = new Command("extract-stars", "Extract STAR segment geometry from a vatSys Airspace.xml file.");
        command.AddOption(airspaceOption);
        command.AddOption(configOption);

        command.SetHandler((airspace, configFile) =>
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();
            var toolsConfig = deserializer.Deserialize<ToolsConfiguration>(File.ReadAllText(configFile.FullName));

            var doc = XDocument.Load(airspace.FullName);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .WithTypeConverter(new FlowSegmentConverter())
                .WithEventEmitter(next => new QuoteNumericStringsEmitter(next))
                .Build();

            foreach (var airportConfig in toolsConfig.Airports)
            {
                var feederFixSet = new HashSet<string>(airportConfig.FeederFixes, StringComparer.OrdinalIgnoreCase);
                var trajectories = ExtractTrajectories(doc, airportConfig.ICAO, feederFixSet, airportConfig);

                var outputPath = airportConfig.Output;
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                    Directory.CreateDirectory(outputDir);

                var yaml = serializer.Serialize(trajectories);
                File.WriteAllText(outputPath, yaml);

                Console.WriteLine($"Wrote {trajectories.Count} trajectories for {airportConfig.ICAO} to {outputPath}");
            }
        }, airspaceOption, configOption);

        return command;
    }

    static List<TrajectoryOutput> ExtractTrajectories(
        XDocument doc,
        string airport,
        HashSet<string> feederFixes,
        AirportToolsConfiguration airportConfig)
    {
        var fixes = BuildFixLookup(doc);
        var runways = BuildRunwayLookup(doc, airport);
        var results = new List<TrajectoryOutput>();

        var stars = doc.Descendants("STAR")
            .Where(s => s.Attribute("Airport")?.Value == airport);

        foreach (var star in stars)
        {
            var starName = star.Attribute("Name")!.Value;
            var approachType = GetApproachType(starName);
            var transitions = star.Elements("Transition").ToList();

            foreach (var route in star.Elements("Route"))
            {
                var runway = route.Attribute("Runway")!.Value;
                var routeText = route.Value.Trim();

                if (string.IsNullOrEmpty(routeText))
                    continue;

                var routeWaypoints = routeText.Split('/');
                if (transitions.Count > 0)
                {
                    // One trajectory per transition × route combination.
                    // The transition name is the feeder fix; the first route waypoint is the transition fix.
                    foreach (var transition in transitions)
                    {
                        var transitionWaypoints = transition.Value.Trim().Split('/');
                        if (transitionWaypoints.Length == 0)
                            continue;

                        var feederFix = transitionWaypoints[0];
                        if (feederFixes.Count > 0 && !feederFixes.Contains(feederFix))
                            continue;

                        if (routeWaypoints.Length == 0)
                            continue;

                        var transitionFix = routeWaypoints[0];
                        var fullSequence = transitionWaypoints.Concat(routeWaypoints).ToArray();

                        var segments = BuildSegments(starName, fullSequence, runway, fixes, runways);
                        if (segments is null)
                            continue;

                        var pressureConfig = FindPressureConfiguration(
                            airportConfig.PressureConfiguration,
                            feederFix,
                            transitionFix,
                            runway,
                            approachType);

                        results.Add(new TrajectoryOutput
                        {
                            FeederFix = feederFix,
                            TransitionFix = transitionFix,
                            RunwayIdentifier = runway,
                            ApproachType = approachType,
                            Segments = segments,
                            PressureAfter = pressureConfig?.Pressure?.After,
                            PressureSegments = ConvertSegments(pressureConfig?.Pressure?.Segments),
                            MaxPressureAfter = pressureConfig?.MaxPressure?.After,
                            MaxPressureSegments = ConvertSegments(pressureConfig?.MaxPressure?.Segments)
                        });
                    }
                }
                else
                {
                    // No transitions — first route waypoint is the feeder fix.
                    if (routeWaypoints.Length < 2)
                        continue;

                    var feederFix = routeWaypoints[0];
                    if (feederFixes.Count > 0 && !feederFixes.Contains(feederFix))
                        continue;

                    var segments = BuildSegments(starName, routeWaypoints, runway, fixes, runways);
                    if (segments is null)
                        continue;

                    var pressureConfig = FindPressureConfiguration(
                        airportConfig.PressureConfiguration,
                        feederFix,
                        null,
                        runway,
                        approachType);

                    results.Add(new TrajectoryOutput
                    {
                        FeederFix = feederFix,
                        RunwayIdentifier = runway,
                        ApproachType = approachType,
                        Segments = segments,
                        PressureAfter = pressureConfig?.Pressure?.After,
                        PressureSegments = ConvertSegments(pressureConfig?.Pressure?.Segments),
                        MaxPressureAfter = pressureConfig?.MaxPressure?.After,
                        MaxPressureSegments = ConvertSegments(pressureConfig?.MaxPressure?.Segments)
                    });
                }
            }
        }

        return results;
    }

    static PressureConfigurationOverride? FindPressureConfiguration(
        PressureConfigurationOverride[] configs,
        string feederFix,
        string? transitionFix,
        string runway,
        string? approachType)
    {
        foreach (var config in configs)
        {
            // Check if feeder fix is in array (multi-fix support)
            if (!config.FeederFixes.Contains(feederFix))
                continue;

            // Match runway, transition, approach type
            if (!string.Equals(config.RunwayIdentifier, runway))
                continue;

            if (config.TransitionFix is not null &&
                !config.TransitionFix.Equals(transitionFix))
                continue;

            if (config.ApproachType is not null &&
                !config.ApproachType.Equals(approachType))
                continue;

            return config;
        }

        return null;
    }

    static List<SegmentOutput> ConvertSegments(SegmentDefinition[]? segments)
    {
        if (segments is null)
            return [];

        return segments.Select(s => new SegmentOutput
        {
            Identifier = s.Identifier,
            Track = s.Track,
            DistanceNM = s.DistanceNM
        }).ToList();
    }

    static List<SegmentOutput>? BuildSegments(
        string starName,
        string[] fixNames,
        string runway,
        Dictionary<string, (double Lat, double Lon)> fixes,
        Dictionary<string, (double Lat, double Lon)> runways)
    {
        var segments = new List<SegmentOutput>();

        // Segments between consecutive fixes in the route
        for (var i = 0; i < fixNames.Length - 1; i++)
        {
            if (!TryGetCoord(fixes, fixNames[i], starName, out var from) ||
                !TryGetCoord(fixes, fixNames[i + 1], starName, out var to))
                return null;

            segments.Add(new SegmentOutput
            {
                Identifier = fixNames[i + 1],
                Track = Math.Round(Calculations.CalculateTrack(from.Lat, from.Lon, to.Lat, to.Lon), 1),
                DistanceNM = Math.Round(Calculations.CalculateDistanceNauticalMiles(from.Lat, from.Lon, to.Lat, to.Lon), 1)
            });
        }

        // Compute a direct segment from the last STAR fix to the runway threshold.
        if (!TryGetCoord(fixes, fixNames[^1], starName, out var lastFix))
            return null;

        if (!runways.TryGetValue(runway, out var rwyCoord))
        {
            Console.Error.WriteLine($"Warning: runway '{runway}' not found in airport positions for STAR {starName}, skipping");
            return null;
        }

        segments.Add(new SegmentOutput
        {
            Identifier = runway,
            Track = Math.Round(Calculations.CalculateTrack(lastFix.Lat, lastFix.Lon, rwyCoord.Lat, rwyCoord.Lon), 1),
            DistanceNM = Math.Round(Calculations.CalculateDistanceNauticalMiles(lastFix.Lat, lastFix.Lon, rwyCoord.Lat, rwyCoord.Lon), 1)
        });

        return segments;
    }

    static bool TryGetCoord(
        Dictionary<string, (double Lat, double Lon)> lookup,
        string name,
        string starName,
        out (double Lat, double Lon) coord)
    {
        if (lookup.TryGetValue(name, out coord))
            return true;

        Console.Error.WriteLine($"Warning: fix '{name}' not found in intersections (STAR {starName}), skipping route");
        return false;
    }

    static Dictionary<string, (double Lat, double Lon)> BuildFixLookup(XDocument doc)
    {
        var lookup = new Dictionary<string, (double Lat, double Lon)>(StringComparer.OrdinalIgnoreCase);
        foreach (var point in doc.Descendants("Point").Where(p => !string.IsNullOrWhiteSpace(p.Value)))
        {
            var name = point.Attribute("Name")!.Value;
            var navaidType = point.Attribute("NavaidType")?.Value;
            var key = navaidType is "NDB" or "VOR" or "TAC" ? $"{name} {navaidType}" : name;
            lookup.TryAdd(key, ParseCoordinate(point.Value.Trim()));
        }
        return lookup;
    }

    static Dictionary<string, (double Lat, double Lon)> BuildRunwayLookup(XDocument doc, string airport)
    {
        return doc.Descendants("Airport")
            .Where(a => a.Attribute("ICAO")?.Value == airport)
            .SelectMany(a => a.Elements("Runway"))
            .Where(r => r.Attribute("Position") is not null)
            .ToDictionary(
                r => r.Attribute("Name")!.Value,
                r => ParseCoordinate(r.Attribute("Position")!.Value.Trim()),
                StringComparer.OrdinalIgnoreCase);
    }

    // Last character of STAR name is the approach type if alphabetical (e.g. BORE4A → "A")
    static string? GetApproachType(string starName) =>
        char.IsLetter(starName[^1]) ? starName[^1].ToString().ToUpperInvariant() : null;

    // Parses a vatSys coordinate string: ±DDMMSS.sss±DDDMMSS.sss
    // Latitude always has a 6-digit integer part (DDMMSS); longitude has 7 (DDDMMSS).
    static (double Lat, double Lon) ParseCoordinate(string text)
    {
        var lonStart = text.IndexOfAny(['+', '-'], 1);
        return (ParseDmsPart(text[..lonStart]), ParseDmsPart(text[lonStart..]));
    }

    static double ParseDmsPart(string part)
    {
        var negative = part[0] == '-';
        var digits = part[1..];

        var dotIndex = digits.IndexOf('.');
        string intPart;
        double fracSeconds;

        if (dotIndex >= 0)
        {
            intPart = digits[..dotIndex];
            fracSeconds = double.Parse("0" + digits[dotIndex..], CultureInfo.InvariantCulture);
        }
        else
        {
            intPart = digits;
            fracSeconds = 0;
        }

        int deg, min;
        double sec;

        if (intPart.Length <= 6)
        {
            // Latitude: DDMMSS (zero-padded to 6 digits)
            intPart = intPart.PadLeft(6, '0');
            deg = int.Parse(intPart[..2]);
            min = int.Parse(intPart[2..4]);
            sec = int.Parse(intPart[4..6]) + fracSeconds;
        }
        else
        {
            // Longitude: DDDMMSS (zero-padded to 7 digits)
            intPart = intPart.PadLeft(7, '0');
            deg = int.Parse(intPart[..3]);
            min = int.Parse(intPart[3..5]);
            sec = int.Parse(intPart[5..7]) + fracSeconds;
        }

        var dd = deg + min / 60.0 + sec / 3600.0;
        return negative ? -dd : dd;
    }
}

// ---------------------------------------------------------------------------
// Serialization
// ---------------------------------------------------------------------------

class FlowSegmentConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(SegmentOutput);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) =>
        throw new NotSupportedException();

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var seg = (SegmentOutput)value!;
        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Flow));
        emitter.Emit(new Scalar("Identifier")); emitter.Emit(QuotedIfNumeric(seg.Identifier));
        emitter.Emit(new Scalar("Track")); emitter.Emit(new Scalar(seg.Track.ToString(CultureInfo.InvariantCulture)));
        emitter.Emit(new Scalar("DistanceNM")); emitter.Emit(new Scalar(seg.DistanceNM.ToString(CultureInfo.InvariantCulture)));
        emitter.Emit(new MappingEnd());
    }

    static Scalar QuotedIfNumeric(string value) =>
        value.Length > 0 && value.All(char.IsDigit)
            ? new Scalar(null, null, value, ScalarStyle.SingleQuoted, false, true)
            : new Scalar(value);
}

class QuoteNumericStringsEmitter(IEventEmitter nextEmitter) : ChainedEventEmitter(nextEmitter)
{
    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        if (eventInfo.Source.Type == typeof(string) &&
            eventInfo.Source.Value is string { Length: > 0 } s &&
            s.All(char.IsDigit))
        {
            eventInfo.Style = ScalarStyle.SingleQuoted;
        }
        base.Emit(eventInfo, emitter);
    }
}

// ---------------------------------------------------------------------------
// Output models
// ---------------------------------------------------------------------------

class TrajectoryOutput
{
    public required string FeederFix { get; init; }
    public required string RunwayIdentifier { get; init; }
    public string? ApproachType { get; init; }
    public string? TransitionFix { get; init; }
    public required List<SegmentOutput> Segments { get; init; }
    public string? PressureAfter { get; init; }
    public List<SegmentOutput> PressureSegments { get; init; } = [];
    public string? MaxPressureAfter { get; init; }
    public List<SegmentOutput> MaxPressureSegments { get; init; } = [];
}

class SegmentOutput
{
    public required string Identifier { get; init; }
    public required double Track { get; init; }
    public required double DistanceNM { get; init; }
}
