using Maestro.Core.Model;

namespace Maestro.Core.Dtos.Configuration;

public class RunwayConfigurationDto(string identifier, int defaultLandingRateSeconds)
{
    public string Identifier { get; } = identifier;
    public int DefaultLandingRateSeconds { get; } = defaultLandingRateSeconds;
}
