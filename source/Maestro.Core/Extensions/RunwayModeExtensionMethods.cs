using Maestro.Contracts.Runway;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class RunwayModeExtensionMethods
{
    public static RunwayModeDto ToDto(this RunwayMode runwayMode)
    {
        return new RunwayModeDto(
            runwayMode.Identifier,
            runwayMode.Runways
                .Select(r =>
                    new RunwayDto(r.Identifier, r.ApproachType, (int)r.AcceptanceRate.TotalSeconds, r.FeederFixes))
                .ToArray(),
            (int)runwayMode.DependencyRate.TotalSeconds,
            (int)runwayMode.OffModeSeparation.TotalSeconds);
    }
}
