namespace TFMS.Core.Dtos.Configuration;

public class RunwayModeConfigurationDTO(string identifier, RunwayConfigurationDTO[] runways)
{
    public string Identifier { get; } = identifier;
    public RunwayConfigurationDTO[] Runways { get; } = runways;
}
