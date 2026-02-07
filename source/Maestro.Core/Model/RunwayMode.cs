using Maestro.Core.Configuration;
using Maestro.Core.Messages;

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
        DependencyRate = TimeSpan.FromSeconds(runwayModeConfigurationConfiguration.DependencyRateSeconds);
        OffModeSeparation = TimeSpan.FromSeconds(runwayModeConfigurationConfiguration.OffModeSeparationSeconds);
    }

    public RunwayMode(RunwayModeDto runwayModeDto)
    {
        Identifier = runwayModeDto.Identifier;
        Runways = runwayModeDto.Runways.Select(r => new Runway(r.Identifier, r.ApproachType, TimeSpan.FromSeconds(r.AcceptanceRateSeconds), r.FeederFixes))
            .ToArray();
    }

    public string Identifier { get; }
    public Runway[] Runways { get; }
    public Runway Default => Runways.First();
    public TimeSpan DependencyRate { get; }
    public TimeSpan OffModeSeparation { get; }
}

public class Runway
{
    public Runway(Configuration.RunwayConfiguration runwayConfiguration)
    {
        Identifier = runwayConfiguration.Identifier;
        ApproachType = runwayConfiguration.ApproachType;
        AcceptanceRate = TimeSpan.FromSeconds(runwayConfiguration.LandingRateSeconds);
        FeederFixes = runwayConfiguration.FeederFixes;
    }

    // DTO ctor. Need to extend for dependencies, requirements, preferences.
    public Runway(string identifier, string approachType, TimeSpan acceptance, string[] feederFixes)
    {
        Identifier = identifier;
        ApproachType = approachType;
        AcceptanceRate = acceptance;
        FeederFixes = feederFixes;
    }

    public string Identifier { get; }
    public string ApproachType { get; }
    public TimeSpan AcceptanceRate { get; private set; }

    public string[] FeederFixes { get; init; } = [];

    public void ChangeAcceptanceRate(TimeSpan acceptanceRate)
    {
        AcceptanceRate = acceptanceRate;
    }
}
