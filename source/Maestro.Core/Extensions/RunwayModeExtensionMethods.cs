using Maestro.Core.Messages;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class RunwayModeExtensionMethods
{
    public static RunwayModeDto ToMessage(this RunwayMode runwayMode)
    {
        return new RunwayModeDto(
            runwayMode.Identifier,
            runwayMode.Runways.ToDictionary(r => r.Identifier, r => (int)r.AcceptanceRate.TotalSeconds));
    }
}
