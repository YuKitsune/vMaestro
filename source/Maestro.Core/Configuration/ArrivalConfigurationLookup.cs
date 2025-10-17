using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public interface IArrivalConfigurationLookup
{
    ArrivalConfiguration[] GetArrivals();
}

public class ArrivalConfigurationLookup : IArrivalConfigurationLookup
{
    readonly string _configurationPath;
    readonly Lazy<ArrivalConfiguration[]> _lazyArrivals;

    public ArrivalConfigurationLookup(string configurationPath)
    {
        _configurationPath = configurationPath;
        _lazyArrivals = new Lazy<ArrivalConfiguration[]>(GetArrivalsInternal);
    }

    public ArrivalConfiguration[] GetArrivals()
    {
        return _lazyArrivals.Value;
    }

    ArrivalConfiguration[] GetArrivalsInternal()
    {
        var lines = File.ReadAllLines(_configurationPath);

        var arrivals = new List<ArrivalConfiguration>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (string.IsNullOrEmpty(line))
                continue;

            if (line.StartsWith("#"))
                continue;

            var columns = line.Split(',');
            if (columns.Length != 9)
                throw new MaestroException($"{_configurationPath} line {i}: Found {columns.Length} columns, expected 9.");

            var airportIdentifier = columns[0].Trim();
            var feederFixIdentifier = columns[1].Trim();
            var transitionFixIdentifier = columns[2].Trim();
            var runwayIdentifier = columns[3].Trim();
            var approachType = columns[4].Trim();
            var aircraftCategory = columns[5].Trim() switch
            {
                "JET" => AircraftCategory.Jet,
                "NONJET" => AircraftCategory.NonJet,
                _ => throw new MaestroException($"{_configurationPath} line {i}: Unexpected aircraft category \"{columns[4]}\"")
            };
            var aircraftTypes = columns[6].Split(';').Select(s => s.Trim()).ToArray();

            if (!int.TryParse(columns[7].Trim(), out var timeToGoSeconds))
                throw new MaestroException($"{_configurationPath} line {i}: Couldn't parse time-to-go \"{columns[7]}\"");

            var timeToGo = TimeSpan.FromSeconds(timeToGoSeconds);

            if (!int.TryParse(columns[8].Trim(), out var trackMiles))
            {
                trackMiles = 0;
            }

            arrivals.Add(new ArrivalConfiguration
            {
                AirportIdentifier = airportIdentifier,
                FeederFixIdentifier = feederFixIdentifier,
                TransitionFixIdentifier = transitionFixIdentifier,
                RunwayIdentifier = runwayIdentifier,
                ApproachType = approachType,
                Category = aircraftCategory,
                AircraftTypes = aircraftTypes,
                TimeToGo = timeToGo,
                TrackMiles = trackMiles,
            });
        }

        return arrivals.ToArray();
    }
}
