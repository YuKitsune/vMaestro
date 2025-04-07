using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Configuration;

namespace Maestro.Plugin.Configuration;

public class PluginConfiguration(
    LoggingConfiguration logging,
    Uri serverUri,
    AirportConfigurationDto[] airports,
    SeparationRuleConfiguration[] separationRules)
{
    public LoggingConfiguration Logging { get; } = logging;
    public Uri ServerUri { get; } = serverUri;
    public AirportConfigurationDto[] Airports { get; } = airports;
    public SeparationRuleConfiguration[] SeparationRules { get; } = separationRules;
}
