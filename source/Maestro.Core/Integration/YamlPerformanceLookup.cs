using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Serilog;

namespace Maestro.Core.Integration;

public class YamlPerformanceLookup(AircraftSpeedProfile[] profiles, ILogger logger) : IPerformanceLookup
{
    public AircraftPerformanceData GetPerformanceDataFor(string aircraftType)
    {
        // Find the first bin that explicitly lists this type code.
        foreach (var profile in profiles)
        {
            var hasExplicitMatch = profile.AircraftTypes
                .OfType<SpecificAircraftTypeDescriptor>()
                .Any(d => string.Equals(d.TypeCode, aircraftType, StringComparison.OrdinalIgnoreCase));

            if (!hasExplicitMatch)
                continue;

            // Infer AircraftCategory from sibling descriptors in the same bin.
            var category = profile.AircraftTypes
                .OfType<AircraftCategoryDescriptor>()
                .Select(d => (AircraftCategory?)d.AircraftCategory)
                .FirstOrDefault();

            return new AircraftPerformanceData(
                aircraftType,
                category ?? AircraftCategory.Jet,
                null);
        }

        return AircraftPerformanceData.Default;
    }

    public SpeedBand[] GetSpeedProfile(AircraftPerformanceData aircraftPerformanceData)
    {
        if (profiles.Length == 0)
            return [];

        var bestScore = 0;
        AircraftSpeedProfile? bestProfile = null;

        foreach (var profile in profiles)
        {
            var score = ScoreProfile(profile, aircraftPerformanceData);
            if (score > bestScore)
            {
                bestScore = score;
                bestProfile = profile;
            }
        }

        if (bestProfile is null)
        {
            logger.Warning(
                "No speed profile matched {AircraftType} ({AircraftCategory}), using first profile",
                aircraftPerformanceData.TypeCode,
                aircraftPerformanceData.AircraftCategory);
            return profiles[0].Speeds;
        }

        return bestProfile.Speeds;
    }

    // Returns 2 for an exact type-code match, 1 for any category/wake/all match, 0 for no match.
    static int ScoreProfile(AircraftSpeedProfile profile, AircraftPerformanceData aircraft)
    {
        var score = 0;
        foreach (var descriptor in profile.AircraftTypes)
        {
            if (!descriptor.Matches(aircraft))
                continue;

            var descriptorScore = descriptor is SpecificAircraftTypeDescriptor ? 2 : 1;
            if (descriptorScore > score)
                score = descriptorScore;
        }
        return score;
    }
}
