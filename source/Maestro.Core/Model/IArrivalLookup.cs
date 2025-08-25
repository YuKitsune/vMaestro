using Maestro.Core.Configuration;
using Serilog;

namespace Maestro.Core.Model;

public interface IArrivalLookup
{
    public TimeSpan? GetArrivalInterval(
        string airportIdentifier,
        string feederFixIdentifier,
        string? arrivalIdentifier,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData);
}

public class ArrivalLookup(IAirportConfigurationProvider airportConfigurationProvider, ILogger logger)
    : IArrivalLookup
{
    public TimeSpan? GetArrivalInterval(
        string airportIdentifier,
        string feederFixIdentifier,
        string? arrivalIdentifier,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .SingleOrDefault(x => x.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            return null;

        var foundArrivalConfigurations = airportConfiguration.Arrivals
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x =>
                (string.IsNullOrEmpty(x.AircraftType) || x.AircraftType == aircraftPerformanceData.Type) &&
                (x.AdditionalAircraftTypes.Contains(aircraftPerformanceData.Type) || x.Category is null || x.Category == aircraftPerformanceData.AircraftCategory) &&
                (string.IsNullOrEmpty(arrivalIdentifier) || x.ArrivalRegex.IsMatch(arrivalIdentifier)))
            .ToArray();

        // No matches, nothing to do
        if (foundArrivalConfigurations.Length == 0)
            return null;

        if (foundArrivalConfigurations.Length > 1)
        {
            // TODO: Show vatSys error
            logger.Warning(
                "Found multiple arrivals with the following lookup parameters: Airport = {AirportIdentifier}; FF = {FeederFix} RWY = {RunwayIdentifier}; STAR = {ArrivalIdentifier}; Type = {Type}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                arrivalIdentifier,
                aircraftPerformanceData.Type);
        }

        return TimeSpan.FromMinutes(foundArrivalConfigurations.First().RunwayIntervals[runwayIdentifier]);
    }
}
