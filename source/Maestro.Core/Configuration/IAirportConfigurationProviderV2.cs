namespace Maestro.Core.Configuration;

public interface IAirportConfigurationProviderV2
{
    /// <summary>
    /// Gets the airport configuration for the specified identifier.
    /// </summary>
    /// <param name="identifier">The airport identifier (ICAO code).</param>
    /// <exception cref="MaestroException">Thrown when no configuration is found for the specified identifier.</exception>
    AirportConfigurationV2 GetAirportConfiguration(string identifier);
}
