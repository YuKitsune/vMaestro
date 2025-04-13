using Maestro.Core.Configuration;

namespace Maestro.Core.Model;

public interface IArrivalLookup
{
    public TimeSpan? GetArrivalInterval(string airportIdentifier, string arrivalIdentifier, string runwayIdentifier);
}

public class ArrivalLookup(IAirportConfigurationProvider airportConfigurationProvider) : IArrivalLookup
{
    public TimeSpan? GetArrivalInterval(string airportIdentifier, string arrivalIdentifier, string runwayIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .SingleOrDefault(x => x.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            return null;
        
        var arrivalConfiguration = airportConfiguration.Arrivals.SingleOrDefault(x => x.ArrivalIdentifier == arrivalIdentifier);
        if (arrivalConfiguration is null)
            return null;

        if (!arrivalConfiguration.RunwayIntervals.TryGetValue(runwayIdentifier, out var runwayIntervalMinutes))
            return null;
        
        return TimeSpan.FromMinutes(runwayIntervalMinutes);
    }
}