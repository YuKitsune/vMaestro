using Maestro.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Model;

public interface IArrivalLookup
{
    public TimeSpan? GetArrivalInterval(
        string airportIdentifier,
        string feederFixIdentifier,
        string? arrivalIdentifier,
        string runwayIdentifier,
        AircraftType aircraftType);
}

public class ArrivalLookup(IAirportConfigurationProvider airportConfigurationProvider, ILogger<ArrivalLookup> logger)
    : IArrivalLookup
{
    public TimeSpan? GetArrivalInterval(
        string airportIdentifier,
        string feederFixIdentifier,
        string? arrivalIdentifier,
        string runwayIdentifier,
        AircraftType aircraftType)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .SingleOrDefault(x => x.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            return null;

        var foundArrivalConfigurations = airportConfiguration.Arrivals
            .Where(x =>
                x.FeederFix == feederFixIdentifier &&
                x.AircraftType == aircraftType &&
                (string.IsNullOrEmpty(arrivalIdentifier) || x.ArrivalRegex.IsMatch(arrivalIdentifier)))
            .Where(x => x.RunwayIntervals.ContainsKey(runwayIdentifier))
            .ToArray();
        
        // No matches, nothing to do
        if (foundArrivalConfigurations.Length == 0)
            return null;

        if (foundArrivalConfigurations.Length > 1)
        {
            // TODO: Show vatSys error
            logger.LogWarning(
                "Found multiple arrivals with the following lookup parameters: Airport = {AirportIdentifier}; FF = {FeederFix} RWY = {RunwayIdentifier}; STAR = {ArrivalIdentifier}; Type = {AircraftType}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                arrivalIdentifier,
                aircraftType);
        }
        
        return TimeSpan.FromMinutes(foundArrivalConfigurations.First().RunwayIntervals[runwayIdentifier]);
    }
}