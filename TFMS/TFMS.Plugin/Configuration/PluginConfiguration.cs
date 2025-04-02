using TFMS.Core.Configuration;
using TFMS.Core.Dtos.Configuration;

namespace TFMS.Plugin.Configuration;

public class PluginConfiguration(LoggingConfiguration logging, Uri serverUri, AirportConfigurationDTO[] airports)
{
    public LoggingConfiguration Logging { get; } = logging;
    public Uri ServerUri { get; } = serverUri;
    public AirportConfigurationDTO[] Airports { get; } = airports;
}
