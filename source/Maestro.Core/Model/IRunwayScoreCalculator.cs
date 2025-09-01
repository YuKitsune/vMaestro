using Maestro.Core.Configuration;

namespace Maestro.Core.Model;

public interface IRunwayScoreCalculator
{
    RunwayScore[] CalculateScores(
        IReadOnlyCollection<RunwayConfiguration> runways,
        string? aircraftType,
        WakeCategory? wakeCategory,
        string? feederFixIdentifier);
}

public record RunwayScore(string RunwayIdentifier, int Score);

public class RunwayScoreCalculator : IRunwayScoreCalculator
{
    // Wake category match is more important than feeder fix match
    const int WakeCategoryScoreWeight = 2;
    const int FeederFixScoreWeight = 1;

    public RunwayScore[] CalculateScores(
        IReadOnlyCollection<RunwayConfiguration> runways,
        string? aircraftType,
        WakeCategory? wakeCategory,
        string? feederFixIdentifier)
    {
        return runways
            .Select(r => new RunwayScore(r.Identifier, CalculatePreferenceScore(r, wakeCategory, feederFixIdentifier)))
            .OrderByDescending(r => r.Score)
            .ToArray();
    }

    private static int CalculatePreferenceScore(
        RunwayConfiguration runway,
        WakeCategory? wakeCategory,
        string? feederFixIdentifier)
    {
        var score = 0;

        if (runway.Preferences is null)
            return score;

        if (wakeCategory is not null && runway.Preferences.WakeCategories.Contains(wakeCategory.Value))
            score += WakeCategoryScoreWeight;

        if (!string.IsNullOrEmpty(feederFixIdentifier) && runway.Preferences.FeederFixes.Contains(feederFixIdentifier))
            score += FeederFixScoreWeight;

        return score;
    }
}
