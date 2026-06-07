using Maestro.Contracts.Runway;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class RunwayModeExtensionMethods
{
    /// <summary>
    ///     Returns a copy of the <paramref name="runwayMode"/> with the landing rates replaced for the runways
    ///     present in <paramref name="newLandingRates"/>. Runways not included retain their existing rate.
    /// </summary>
    public static RunwayMode WithLandingRates(this RunwayMode runwayMode, IReadOnlyDictionary<string, TimeSpan> newLandingRates)
    {
        var runways = runwayMode.Runways
            .Select(r => new Runway(
                r.Identifier,
                r.ApproachType,
                newLandingRates.TryGetValue(r.Identifier, out var rate) ? rate : r.AcceptanceRate,
                r.FeederFixes))
            .ToArray();

        return new RunwayMode(runwayMode.Identifier, runways, runwayMode.DependencyRate, runwayMode.OffModeSeparation);
    }

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
