using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class RunwayConfiguration
{
    public required string Identifier { get; init; }
    public required int LandingRateSeconds { get; init; }
    public RunwayDependency[] Dependencies { get; init; } = [];
    public RunwayRequirements? Requirements { get; init; }
    public RunwayPreferences? Preferences { get; init; }
}

// BUG: This won't work when a STAR doesn't terminate at all runways (i.e. Brisbane)

public class RunwayRequirements
{
    public string[] FeederFixes { get; set; } = [];
}

public class RunwayPreferences
{
    public WakeCategory[] WakeCategories { get; set; } = [];
    public string[] FeederFixes { get; set; } = [];
}

public class RunwayDependency
{
    public required string RunwayIdentifier { get; init; }
    public int SeparationSeconds { get; init; }
}
