namespace Maestro.Core.Configuration;

public interface IAirportConfigurationProvider
{
    AirportConfiguration[] GetAirportConfigurations();
}

public class AirportConfigurationProvider(AirportConfiguration[] airportConfigurations) : IAirportConfigurationProvider
{
    public AirportConfiguration[] GetAirportConfigurations()
    {
        return airportConfigurations;
    }
}
