using Maestro.Core.Configuration;
using Serilog;

namespace Maestro.Core.Model;

public interface IArrivalLookup
{
    public TimeSpan? GetArrivalInterval(
        string airportIdentifier,
        string feederFixIdentifier,
        string[] fixNames,
        string approachType,
        string runwayIdentifier,
        string aircraftTypeCode,
        AircraftCategory aircraftCategory);

    public string[] GetApproachTypes(
        string airportIdentifier,
        string feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        string aircraftTypeCode,
        AircraftCategory aircraftCategory);
}

public static class ArrivalLookupExtensionMethods
{
    public static TimeSpan? GetArrivalInterval(this IArrivalLookup arrivalLookup, Flight flight)
    {
        return arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.Fixes.Select(x => x.FixIdentifier).ToArray(),
            flight.ApproachType,
            flight.AssignedRunwayIdentifier,
            flight.AircraftType,
            flight.AircraftCategory);
    }
}

public class ArrivalLookup(IAirportConfigurationProvider airportConfigurationProvider, ILogger logger)
    : IArrivalLookup
{
    public TimeSpan? GetArrivalInterval(
        string airportIdentifier,
        string feederFixIdentifier,
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

        // No matches, nothing to do
        if (foundArrivalConfigurations.Length == 0)
            return null;

        if (foundArrivalConfigurations.Length > 1)
        {
            // TODO: Show vatSys error
            logger.Warning(
                "Found multiple arrivals with the following lookup parameters: Airport = {AirportIdentifier}; FF = {FeederFix} RWY = {RunwayIdentifier}; APCH = {ApproachType}; Type = {Type}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                approachType,
                aircraftTypeCode);
        }

        return TimeSpan.FromMinutes(foundArrivalConfigurations.First().RunwayIntervals[runwayIdentifier]);

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
        string feederFixIdentifier,
        string[] fixNames,
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
            .Where(x => x.RunwayIntervals.ContainsKey(runwayIdentifier))
            .Where(x => string.IsNullOrEmpty(x.ApproachFix) || fixNames.Contains(x.ApproachFix))
            .Where(x =>
                ((string.IsNullOrEmpty(x.AircraftType) || x.AircraftType == aircraftTypeCode) &&
                 (x.Category is null || x.Category == aircraftCategory)) ||
                x.AdditionalAircraftTypes.Contains(aircraftTypeCode))
            .OrderByDescending(GetRank)
            .ToArray();

        // No matches, nothing to do
        if (foundArrivalConfigurations.Length == 0)
            return null;

        if (foundArrivalConfigurations.Length > 1)
        {
            // TODO: Show vatSys error
            logger.Warning(
                "Found multiple arrivals with the following lookup parameters: Airport = {AirportIdentifier}; FF = {FeederFix} RWY = {RunwayIdentifier}; Type = {Type}",
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
}
