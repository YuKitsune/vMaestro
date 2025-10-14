using Maestro.Core.Configuration;

namespace Maestro.Plugin.Configuration;

public class PluginConfiguration
{
    public required ServerConfiguration Server { get; init; }
    public required LoggingConfiguration Logging { get; init; }
    public required string ArrivalConfigurationPath { get; init; }
    public required AirportConfiguration[] Airports { get; init; }
}
