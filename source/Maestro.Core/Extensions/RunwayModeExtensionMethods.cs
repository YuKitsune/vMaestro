using Maestro.Core.Messages;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class RunwayModeExtensionMethods
{
    public static RunwayModeDto ToMessage(this RunwayMode runwayMode)
    {
        return new RunwayModeDto(
            runwayMode.Identifier,
            runwayMode.Runways
                .Select(r =>
                    new RunwayDto(r.Identifier, r.ApproachType, (int)r.AcceptanceRate.TotalSeconds, r.FeederFixes))
                .ToArray());
    }
}
