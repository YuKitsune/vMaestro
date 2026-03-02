using Maestro.Core.Configuration;

namespace Maestro.Plugin.Configuration;

public class PluginConfiguration
{
    public required ServerConfiguration Server { get; init; }
    public required LoggingConfiguration Logging { get; init; }
    public required AirportConfiguration[] Airports { get; init; }
    public required CoordinationMessageConfiguration CoordinationMessages { get; init; }
}

public class PluginConfigurationV2
{
    public required ServerConfiguration Server { get; init; }
    public required LoggingConfiguration Logging { get; init; }
    public required AirportConfigurationV2[] Airports { get; init; }
    public required ColourConfigurationV2 Colours { get; init; }
    public required LabelsConfigurationV2 Labels { get; init; }
}
