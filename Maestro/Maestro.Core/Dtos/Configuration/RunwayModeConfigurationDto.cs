namespace Maestro.Core.Dtos.Configuration;

public class RunwayModeConfigurationDto(string identifier, TimeSpan staggerRate, RunwayConfigurationDto[] runways, Dictionary<string, TimeSpan> landingRates, RunwayAssignmentRuleDto[] assignmentRules)
{
    public string Identifier { get; } = identifier;
    public TimeSpan StaggerRate { get; } = staggerRate;
    public RunwayConfigurationDto[] Runways { get; } = runways;
    public Dictionary<string, TimeSpan> LandingRates { get; } = landingRates;
    public RunwayAssignmentRuleDto[] AssignmentRules { get; } = assignmentRules;
}
