using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Configuration;

namespace Maestro.Plugin.Configuration;

public class PluginConfiguration(LoggingConfiguration logging, Uri serverUri, AirportConfigurationDTO[] airports)
{
    public LoggingConfiguration Logging { get; } = logging;
    public Uri ServerUri { get; } = serverUri;
    public AirportConfigurationDTO[] Airports { get; } = airports;
}
