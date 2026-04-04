using System.Text;
using Maestro.Contracts.Runway;
using Maestro.Core.Configuration;

namespace Maestro.Core.Model;

// TODO: Need to clean this up a good bit.
// Mixing Domain types and config in here makes the tests real messy.

public class RunwayMode
{
    public RunwayMode(RunwayModeConfiguration runwayModeConfigurationConfiguration)
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
        DependencyRate = TimeSpan.FromSeconds(runwayModeDto.DependencyRateSeconds);
        OffModeSeparation = TimeSpan.FromSeconds(runwayModeDto.OffModeSeparationSeconds);
    }

    public string Identifier { get; }
    public Runway[] Runways { get; }
    public Runway Default => Runways.First();
    public TimeSpan DependencyRate { get; }
    public TimeSpan OffModeSeparation { get; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append(Identifier);
        sb.Append(" (");
        foreach (var runway in Runways)
        {
            sb.Append(runway.Identifier);
            sb.Append(": ");
            sb.Append(runway.AcceptanceRate.TotalSeconds);
            sb.Append("s; ");
        }

        sb.Append($") OffMode: {OffModeSeparation.TotalSeconds}s Dependency: {DependencyRate.TotalSeconds}s");

        return sb.ToString();
    }
}

public class Runway
{
    public Runway(RunwayConfiguration runwayConfiguration)
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

    public string[] FeederFixes { get; init; }

    public void ChangeAcceptanceRate(TimeSpan acceptanceRate)
    {
        AcceptanceRate = acceptanceRate;
    }
}
