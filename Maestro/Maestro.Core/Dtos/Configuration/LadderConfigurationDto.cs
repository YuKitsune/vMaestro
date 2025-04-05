using Maestro.Core.Model;

namespace Maestro.Core.Dtos.Configuration;

public class LadderConfigurationDto(string[]? feederFixes, string[]? runways)
{
    public string[]? FeederFixes { get; } = feederFixes;
    public string[]? Runways { get; } = runways;
}
