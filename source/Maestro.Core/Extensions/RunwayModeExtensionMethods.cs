using Maestro.Core.Configuration;
using Maestro.Core.Handlers;

namespace Maestro.Core.Extensions;

public static class RunwayModeExtensionMethods
{
    public static RunwayModeDto ToMessage(this RunwayMode runwayMode)
    {
        return new RunwayModeDto(
            runwayMode.Identifier,
            runwayMode.Runways.Select(r =>
                    new RunwayConfigurationDto(r.Identifier, r.LandingRateSeconds))
                .ToArray());
    }
}