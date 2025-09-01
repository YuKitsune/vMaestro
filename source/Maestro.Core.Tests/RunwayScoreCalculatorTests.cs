using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Shouldly;

namespace Maestro.Core.Tests;

public class RunwayScoreCalculatorTests
{
    readonly RunwayScoreCalculator _runwayScoreCalculator;
    readonly RunwayConfiguration[] _runways;

    public RunwayScoreCalculatorTests()
    {
        _runwayScoreCalculator = new RunwayScoreCalculator();
        _runways =
        [
            new RunwayConfiguration
            {
                Identifier = "34L",
                LandingRateSeconds = 180,
                Preferences = new RunwayPreferences
                {
                    WakeCategories = [WakeCategory.Heavy, WakeCategory.SuperHeavy],
                    FeederFixes = ["RIVET", "WELSH"]
                }
            },
            new RunwayConfiguration
            {
                Identifier = "34R",
                LandingRateSeconds = 180,
                Preferences = new RunwayPreferences
                {
                    FeederFixes = ["BOREE", "YAKKA", "MARLN"]
                }
            },
            new RunwayConfiguration
            {
                Identifier = "16R",
                LandingRateSeconds = 180,
                Preferences = new RunwayPreferences
                {
                    WakeCategories = [WakeCategory.Heavy, WakeCategory.SuperHeavy],
                    FeederFixes = ["RIVET", "WELSH"]
                }
            },
            new RunwayConfiguration
            {
                Identifier = "16L",
                LandingRateSeconds = 180,
                Preferences = new RunwayPreferences
                {
                    FeederFixes = ["BOREE", "YAKKA", "MARLN"]
                }
            }
        ];
    }

    [Fact]
    public void WhenWakeCategoryMatches_RunwayShouldBePreferred()
    {
        var results = _runwayScoreCalculator.CalculateScores(_runways, "B744", WakeCategory.Heavy, "BOREE");

        results.Single(r => r.RunwayIdentifier == "34L").Score.ShouldBe(2); // Wake match, but no feeder match
        results.Single(r => r.RunwayIdentifier == "16R").Score.ShouldBe(2); // Wake match, but no feeder match
        results.Single(r => r.RunwayIdentifier == "34R").Score.ShouldBe(1); // Feeder match, but no wake match
        results.Single(r => r.RunwayIdentifier == "16L").Score.ShouldBe(1); // Feeder match, but no wake match
    }

    [Fact]
    public void WhenFeederFixMatches_RunwayShouldBePreferred()
    {
        var results = _runwayScoreCalculator.CalculateScores(_runways, "B738", WakeCategory.Medium, "BOREE");

        results.Single(r => r.RunwayIdentifier == "34R").Score.ShouldBe(1); // Feeder match, but no wake match
        results.Single(r => r.RunwayIdentifier == "16L").Score.ShouldBe(1); // Feeder match, but no wake match
        results.Single(r => r.RunwayIdentifier == "34L").Score.ShouldBe(0);
        results.Single(r => r.RunwayIdentifier == "16R").Score.ShouldBe(0);
    }

    [Fact]
    public void WhenNothingMatches_NoScoreIsAssigned()
    {
        var results = _runwayScoreCalculator.CalculateScores(_runways, "C172", WakeCategory.Light, string.Empty);
        results.ShouldAllBe(r => r.Score == 0);
    }
}
