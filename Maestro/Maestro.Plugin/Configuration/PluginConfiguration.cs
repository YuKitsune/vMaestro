using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Configuration;

namespace Maestro.Plugin.Configuration;

public class PluginConfiguration
{
    public required LoggingConfiguration Logging { get; init; }
    public required Uri ServerUri { get; init; }
    public required FeederFixEstimateSource FeederFixEstimateSource { get; init; }
    public required LandingEstimateSource LandingEstimateSource { get; init; }
    public required AirportConfiguration[] Airports { get; init; }
    public required SeparationRule[] SeparationRules { get; init; }
}