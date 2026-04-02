using System.CommandLine;
using System.Globalization;
using System.Xml.Linq;

namespace Maestro.Tools.Commands;

public static class ExtractPerformanceCommand
{
    const int DefaultMaxLevel = 10_000;

    public static Command Build()
    {
        var performanceOption = new Option<FileInfo>("--performance", "Path to vatSys Performance.xml") { IsRequired = true };
        var outputOption = new Option<FileInfo>("--output", "Output YAML file path") { IsRequired = true };
        var maxLevelOption = new Option<int>("--max-level", () => DefaultMaxLevel, "Maximum altitude in feet; descent speeds above this level are excluded");

        var command = new Command("extract-performance", "Extract aircraft performance data from a vatSys Performance.xml file.");
        command.AddOption(performanceOption);
        command.AddOption(outputOption);
        command.AddOption(maxLevelOption);

        command.SetHandler((performance, output, maxLevel) =>
        {
            var doc = XDocument.Load(performance.FullName);
            var entries = ExtractPerformance(doc, maxLevel);

            var lines = entries.Select(e =>
                $"- {{TypeCode: {e.TypeCode}, DescentSpeedKnots: {e.DescentSpeedKnots}, IsJet: {e.IsJet.ToString().ToLower()}, WakeCategory: {e.WakeCategory}}}");
            File.WriteAllText(output.FullName, string.Join("\n", lines) + "\n");

            Console.WriteLine($"Wrote {entries.Count} aircraft performance entries to {output.FullName}");
        }, performanceOption, outputOption, maxLevelOption);

        return command;
    }

    static List<AircraftPerformanceOutput> ExtractPerformance(XDocument doc, int maxLevel)
    {
        var results = new List<AircraftPerformanceOutput>();

        foreach (var perfData in doc.Descendants("PerformanceData"))
        {
            var isJet = bool.Parse(perfData.Attribute("IsJet")!.Value);

            var values = perfData.Element("Values");
            if (values is null) continue;

            var levels = (values.Attribute("Levels")?.Value ?? string.Empty)
                .Split(',')
                .Select(int.Parse)
                .ToArray();

            var descentSpeeds = (values.Element("DescentSpeeds")?.Value ?? string.Empty)
                .Split(',');

            // Collect indices where the level (in hundreds of feet) is at or below maxLevel
            var validIndices = levels
                .Select((level, i) => (level, i))
                .Where(x => x.level * 100 <= maxLevel)
                .Select(x => x.i)
                .ToArray();

            if (validIndices.Length == 0) continue;

            var avgSpeed = (int)Math.Round(
                validIndices.Average(i => ParseSpeedKnots(descentSpeeds[i], levels[i])));

            var fallbackCategory = MapWakeCategory(perfData.Attribute("PerfCat")?.Value ?? "M");

            var typesText = perfData.Element("Types")?.Value ?? string.Empty;
            foreach (var entry in typesText.Split(','))
            {
                var trimmed = entry.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string typeCode, wakeCategory;
                var slash = trimmed.IndexOf('/');
                if (slash >= 0)
                {
                    typeCode = trimmed[..slash];
                    wakeCategory = MapWakeCategory(trimmed[(slash + 1)..]);
                }
                else
                {
                    typeCode = trimmed;
                    wakeCategory = fallbackCategory;
                }

                if (string.IsNullOrEmpty(typeCode)) continue;

                results.Add(new AircraftPerformanceOutput
                {
                    TypeCode = typeCode,
                    DescentSpeedKnots = avgSpeed,
                    IsJet = isJet,
                    WakeCategory = wakeCategory
                });
            }
        }

        return results;
    }

    // Speed format: N0000 = TAS in knots (N0220 → 220 kt), M000 = Mach (M082 → 0.82)
    static double ParseSpeedKnots(string speed, int levelHundredsFt)
    {
        if (speed.StartsWith('N'))
            return double.Parse(speed[1..], CultureInfo.InvariantCulture);

        if (speed.StartsWith('M'))
        {
            var mach = double.Parse(speed[1..], CultureInfo.InvariantCulture) / 100.0;
            var altitudeFt = levelHundredsFt * 100.0;
            var altitudeM = altitudeFt * 0.3048;
            var tempK = altitudeFt < 36_089 ? 288.15 - 0.0065 * altitudeM : 216.65;
            var speedOfSoundKnots = 661.4786 * Math.Sqrt(tempK / 288.15);
            return mach * speedOfSoundKnots;
        }

        throw new FormatException($"Unrecognised speed format: '{speed}'");
    }

    // J = SuperHeavy, H = Heavy, M = Medium, L = Light
    static string MapWakeCategory(string cat) => cat.ToUpperInvariant() switch
    {
        "J" => "SuperHeavy",
        "H" => "Heavy",
        "M" => "Medium",
        "L" => "Light",
        _ => cat
    };
}

// ---------------------------------------------------------------------------
// Output models
// ---------------------------------------------------------------------------

class AircraftPerformanceOutput
{
    public required string TypeCode { get; init; }
    public required int DescentSpeedKnots { get; init; }
    public required bool IsJet { get; init; }
    public required string WakeCategory { get; init; }
}
