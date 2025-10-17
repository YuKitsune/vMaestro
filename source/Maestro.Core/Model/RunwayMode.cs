using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public class RunwayMode
{
    public RunwayMode(Configuration.RunwayModeConfiguration configuration)
    {
        Identifier = configuration.Identifier;
        Runways = configuration.Runways
            .Select(r => new Runway(r))
            .ToArray();
        OffModeSeparation = TimeSpan.FromSeconds(configuration.OffModeSeparationSeconds);
    }

    public RunwayMode(RunwayModeDto dto)
    {
        Identifier = dto.Identifier;;
        Runways = dto.Runways
            .Select(r => new Runway(r))
            .ToArray();
        OffModeSeparation = TimeSpan.FromSeconds(dto.OffModeSeparationSeconds);
    }

    public string Identifier { get; }
    public Runway[] Runways { get; }
    public Runway Default => Runways.First();
    public TimeSpan OffModeSeparation { get; }
}

public class Runway
{
    public Runway(Configuration.RunwayConfiguration configuration)
    {
        Identifier = configuration.Identifier;
        ApproachType = configuration.ApproachType;
        AcceptanceRate = TimeSpan.FromSeconds(configuration.LandingRateSeconds);
        Dependencies = configuration.Dependencies
            .Select(d => new RunwayDependency(d))
            .ToArray();
    }

    public Runway(RunwayDto dto)
    {
        Identifier = dto.Identifier;
        ApproachType = dto.ApproachType;
        AcceptanceRate = TimeSpan.FromSeconds(dto.AcceptanceRateSeconds);
        Dependencies = dto.Dependencies
            .Select(d => new RunwayDependency(d))
            .ToArray();
    }

    public Runway(string identifier, string approachType, TimeSpan acceptanceRate, RunwayDependency[] dependencies)
    {
        Identifier = identifier;
        ApproachType = approachType;
        AcceptanceRate = acceptanceRate;
        Dependencies = dependencies;
    }

    public string Identifier { get; }
    public string ApproachType { get; }
    public TimeSpan AcceptanceRate { get; }
    public RunwayDependency[] Dependencies { get; }
}

public class RunwayDependency
{
    public RunwayDependency(Configuration.RunwayDependency configuration)
    {
        RunwayIdentifier = configuration.RunwayIdentifier;
        Separation = TimeSpan.FromSeconds(configuration.SeparationSeconds);
    }

    public RunwayDependency(RunwayDependencyDto dto)
    {
        RunwayIdentifier = dto.RunwayIdentifier;
        Separation = TimeSpan.FromSeconds(dto.SeparationSeconds);
    }

    public RunwayDependency(string runwayIdentifier, TimeSpan separation)
    {
        RunwayIdentifier = runwayIdentifier;
        Separation = separation;
    }

    public string RunwayIdentifier { get; }
    public TimeSpan Separation { get; }
}
