namespace Maestro.Core.Configuration;

public interface IAirportConfigurationProvider
{
    /// <summary>
    /// Gets the airport configuration for the specified identifier.
    /// </summary>
    /// <param name="identifier">The airport identifier (ICAO code).</param>
    /// <exception cref="MaestroException">Thrown when no configuration is found for the specified identifier.</exception>
    AirportConfiguration GetAirportConfiguration(string identifier);
}

public class AirportConfigurationProvider(AirportConfiguration[] airportConfigurations)
    : IAirportConfigurationProvider
{
    private readonly Dictionary<string, AirportConfiguration> _configurationLookup =
        airportConfigurations.ToDictionary(
            config => config.Identifier,
            StringComparer.OrdinalIgnoreCase);

    public AirportConfiguration GetAirportConfiguration(string identifier)
    {
        if (_configurationLookup.TryGetValue(identifier, out var configuration))
        {
            return configuration;
        }

        throw new MaestroException($"No configuration found for airport {identifier}");
    }
}
