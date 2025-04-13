namespace Maestro.Core.Configuration;

public class LadderConfiguration(string[]? feederFixes, string[]? runways)
{
    public string[]? FeederFixes { get; } = feederFixes;
    public string[]? Runways { get; } = runways;
}
