namespace Maestro.Core.Dtos.Configuration;

public class LadderConfigurationDTO(string[]? feederFixes, string[]? runways)
{
    public string[]? FeederFixes { get; } = feederFixes;
    public string[]? Runways { get; } = runways;
}
