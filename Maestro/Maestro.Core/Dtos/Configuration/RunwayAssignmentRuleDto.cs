namespace Maestro.Core.Dtos.Configuration;

public class RunwayAssignmentRuleDto(
    string name,
    string runwayIdentifier,
    bool jets,
    bool nonJets,
    bool heavy,
    bool medium,
    bool light,
    string[] feederFixes,
    int priority)
{
    public string Name { get; } = name;
    public string RunwayIdentifier { get; } = runwayIdentifier;
    public bool Jets { get; } = jets;
    public bool NonJets { get; } = nonJets;
    public bool Heavy { get; } = heavy;
    public bool Medium { get; } = medium;
    public bool Light { get; } = light;
    public string[] FeederFixes { get; } = feederFixes;
    public int Priority { get; } = priority;
}