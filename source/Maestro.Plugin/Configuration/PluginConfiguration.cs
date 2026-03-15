using Maestro.Core.Configuration;

namespace Maestro.Plugin.Configuration;

public class PluginConfiguration
{
    public bool CheckForUpdates { get; set; } = true;
    public required ServerConfiguration Server { get; init; }
    public required LoggingConfiguration Logging { get; init; }
    public required AirportConfiguration[] Airports { get; init; }
    public required LabelsConfiguration Labels { get; init; }
}
