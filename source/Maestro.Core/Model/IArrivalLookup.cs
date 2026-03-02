using Maestro.Core.Configuration;
using Serilog;

namespace Maestro.Core.Model;

public interface IArrivalLookup
{
    Trajectory? GetTrajectory(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string approachType,
        string runwayIdentifier,
        string aircraftTypeCode,
        AircraftCategory aircraftCategory);

    string[] GetApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        string aircraftTypeCode,
        AircraftCategory aircraftCategory);

    Trajectory GetAverageTrajectory(string airportIdentifier);
}

public static class ArrivalLookupExtensionMethods
{
    public static Trajectory? GetTrajectory(
        this IArrivalLookup arrivalLookup,
        Flight flight,
        string runwayIdentifier,
        string approachType)
    {
        return arrivalLookup.GetTrajectory(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.Fixes.Select(x => x.FixIdentifier).ToArray(),
            approachType,
            runwayIdentifier,
            flight.AircraftType,
            flight.AircraftCategory);
    }
}

public class ArrivalLookup(IAirportConfigurationProvider airportConfigurationProvider, ILogger logger)
    : IArrivalLookup
{
    public Trajectory? GetTrajectory(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string approachType,
        string runwayIdentifier,
        string aircraftTypeCode,
        AircraftCategory aircraftCategory)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .SingleOrDefault(x => x.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            return null;

        var foundArrivalConfigurations = airportConfiguration.Arrivals
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x => x.ApproachType == approachType)
            .Where(x => string.IsNullOrEmpty(x.ApproachFix) || fixNames.Contains(x.ApproachFix))
            .Where(x =>
                ((string.IsNullOrEmpty(x.AircraftType) || x.AircraftType == aircraftTypeCode) &&
                 (x.Category is null || x.Category == aircraftCategory)) ||
                x.AdditionalAircraftTypes.Contains(aircraftTypeCode))
            .OrderByDescending(GetRank)
            .ToArray();

        // No matches, return null (caller should use GetAverageTrajectory as fallback)
        if (foundArrivalConfigurations.Length == 0)
        {
            logger.Warning(
                "No trajectory found: Airport={AirportIdentifier}, FF={FeederFix}, RWY={RunwayIdentifier}, APCH={ApproachType}, Type={Type}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                approachType,
                aircraftTypeCode);

            return null;
        }

        if (foundArrivalConfigurations.Length > 1)
        {
            logger.Warning(
                "Multiple trajectories found: Airport={AirportIdentifier}, FF={FeederFix}, RWY={RunwayIdentifier}, APCH={ApproachType}, Type={Type}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                approachType,
                aircraftTypeCode);
        }

        var arrivalConfig = foundArrivalConfigurations.First();
        if (!arrivalConfig.RunwayIntervals.TryGetValue(runwayIdentifier, out var ttgMinutes))
            return null;

        return new Trajectory(TimeSpan.FromMinutes(ttgMinutes));

        int GetRank(ArrivalConfiguration arrivalConfiguration)
        {
            var rank = 0;
            if (!string.IsNullOrEmpty(arrivalConfiguration.ApproachType) &&
                arrivalConfiguration.ApproachType == approachType)
            {
                rank++;
            }

            if (!string.IsNullOrEmpty(arrivalConfiguration.ApproachFix) &&
                fixNames.Contains(arrivalConfiguration.ApproachFix))
            {
                rank++;
            }

            return rank;
        }
    }

    public string[] GetApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        string aircraftTypeCode,
        AircraftCategory aircraftCategory)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .SingleOrDefault(x => x.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            return [];

        var foundArrivalConfigurations = airportConfiguration.Arrivals
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x => x.RunwayIntervals.ContainsKey(runwayIdentifier))
            .Where(x => string.IsNullOrEmpty(x.ApproachFix) || fixNames.Contains(x.ApproachFix))
            .Where(x =>
                ((string.IsNullOrEmpty(x.AircraftType) || x.AircraftType == aircraftTypeCode) &&
                 (x.Category is null || x.Category == aircraftCategory)) ||
                x.AdditionalAircraftTypes.Contains(aircraftTypeCode))
            .OrderByDescending(GetRank)
            .ToArray();

        if (foundArrivalConfigurations.Length == 0)
            return [];

        if (foundArrivalConfigurations.Length > 1)
        {
            logger.Warning(
                "Multiple approach types found: Airport={AirportIdentifier}, FF={FeederFix}, RWY={RunwayIdentifier}, Type={Type}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                aircraftTypeCode);
        }

        return foundArrivalConfigurations.Select(a => a.ApproachType).ToArray();

        int GetRank(ArrivalConfiguration arrivalConfiguration)
        {
            var rank = 0;
            if (!string.IsNullOrEmpty(arrivalConfiguration.ApproachFix) &&
                fixNames.Contains(arrivalConfiguration.ApproachFix))
            {
                rank++;
            }

            return rank;
        }
    }

    public Trajectory GetAverageTrajectory(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .SingleOrDefault(x => x.Identifier == airportIdentifier);

        if (airportConfiguration is null || airportConfiguration.Arrivals.Length == 0)
        {
            logger.Warning("No airport configuration for {AirportIdentifier}, using default TTG", airportIdentifier);
            return new Trajectory(TimeSpan.FromMinutes(20));
        }

        var allIntervals = airportConfiguration.Arrivals
            .SelectMany(a => a.RunwayIntervals.Values)
            .ToList();

        if (allIntervals.Count == 0)
        {
            logger.Warning("No arrival intervals for {AirportIdentifier}, using default TTG", airportIdentifier);
            return new Trajectory(TimeSpan.FromMinutes(20));
        }

        var averageTtg = TimeSpan.FromMinutes(allIntervals.Average());
        return new Trajectory(averageTtg);
    }
}
