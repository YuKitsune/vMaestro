namespace Maestro.Core.Model;

// TODO: Need to clean this up a good bit.
// Mixing Domain types and config in here makes the tests real messy.

public class RunwayMode
{
    public RunwayMode(Configuration.RunwayModeConfiguration runwayModeConfigurationConfiguration)
    {
        Identifier = runwayModeConfigurationConfiguration.Identifier;
        Runways = runwayModeConfigurationConfiguration.Runways
            .Select(r => new Runway(r))
            .ToArray();
    }

    public string Identifier { get; }
    public Runway[] Runways { get; }
    public Runway Default => Runways.First();
}

public class Runway
{
    public Runway(Configuration.RunwayConfiguration runwayConfiguration)
    {
        Identifier = runwayConfiguration.Identifier;
        AcceptanceRate = TimeSpan.FromSeconds(runwayConfiguration.LandingRateSeconds);
        Dependencies = runwayConfiguration.Dependencies
            .Select(d => new RunwayDependency(d))
            .ToArray();
        Requirements = runwayConfiguration.Requirements is not null
            ? new RunwayRequirements(runwayConfiguration.Requirements)
            : null;
        Preferences = runwayConfiguration.Preferences is not null
            ? new RunwayPreferences(runwayConfiguration.Preferences)
            : null;
    }

    public string Identifier { get; }
    public TimeSpan AcceptanceRate { get; private set; }
    public RunwayDependency[] Dependencies { get; }
    public RunwayRequirements? Requirements { get; }
    public RunwayPreferences? Preferences { get; }

    public void ChangeAcceptanceRate(TimeSpan acceptanceRate)
    {
        AcceptanceRate = acceptanceRate;
    }
}

public class RunwayDependency(Configuration.RunwayDependency runwayDependency)
{
    public string RunwayIdentifier { get; } = runwayDependency.RunwayIdentifier;

    public TimeSpan? Separation { get; } = runwayDependency.SeparationSeconds.HasValue
        ? TimeSpan.FromSeconds(runwayDependency.SeparationSeconds.Value)
        : null;
}

public class RunwayRequirements(Configuration.RunwayRequirements runwayRequirements)
{
    public string[] FeederFixes { get; } = runwayRequirements.FeederFixes;
}

public class RunwayPreferences(Configuration.RunwayPreferences runwayPreferences)
{
    public WakeCategory[] WakeCategories { get; } = runwayPreferences.WakeCategories;
    public string[] FeederFixes { get; } = runwayPreferences.FeederFixes;
}
