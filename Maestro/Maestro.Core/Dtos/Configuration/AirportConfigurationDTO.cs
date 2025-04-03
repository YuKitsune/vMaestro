namespace Maestro.Core.Dtos.Configuration;

public class AirportConfigurationDTO(string identifier, RunwayConfigurationDTO[] runways, RunwayModeConfigurationDTO[] runwayModes, ViewConfigurationDTO[] views, string[] feederFixes)
{
    public string Identifier { get; } = identifier;
    public RunwayConfigurationDTO[] Runways { get; } = runways;
    public RunwayModeConfigurationDTO[] RunwayModes { get; } = runwayModes;
    public ViewConfigurationDTO[] Views { get; } = views;
    public string[] FeederFixes { get; } = feederFixes;
}
