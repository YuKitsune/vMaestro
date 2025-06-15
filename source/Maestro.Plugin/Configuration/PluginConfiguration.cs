using Maestro.Core.Configuration;

namespace Maestro.Plugin.Configuration;

public class PluginConfiguration
{
    public required LoggingConfiguration Logging { get; init; }
    public required MaestroConfiguration Maestro { get; init; }
    public required AirportConfiguration[] Airports { get; init; }
}