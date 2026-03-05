namespace Maestro.Core.Configuration;

public class AirportConfigurationProviderV2(AirportConfigurationV2[] airportConfigurations)
    : IAirportConfigurationProviderV2
{
    private readonly Dictionary<string, AirportConfigurationV2> _configurationLookup =
        airportConfigurations.ToDictionary(
            config => config.Identifier,
            StringComparer.OrdinalIgnoreCase);

    public AirportConfigurationV2 GetAirportConfiguration(string identifier)
    {
        if (_configurationLookup.TryGetValue(identifier, out var configuration))
        {
            return configuration;
        }

        throw new MaestroException($"No configuration found for airport {identifier}");
    }
}
