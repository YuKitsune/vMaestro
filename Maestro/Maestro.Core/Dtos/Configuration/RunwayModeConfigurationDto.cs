namespace Maestro.Core.Dtos.Configuration;

public class RunwayModeConfigurationDto(string identifier, RunwayConfigurationDto[] runways)
{
    public string Identifier { get; } = identifier;
    public RunwayConfigurationDto[] Runways { get; } = runways;
}
