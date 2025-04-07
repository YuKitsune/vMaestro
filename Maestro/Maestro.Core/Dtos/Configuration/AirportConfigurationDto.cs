namespace Maestro.Core.Dtos.Configuration;

public class AirportConfigurationDto(
    string identifier,
    RunwayConfigurationDto[] runways,
    RunwayModeConfigurationDto[] runwayModes,
    ViewConfigurationDto[] views,
    string[] feederFixes)
{
    public string Identifier { get; } = identifier;
    public RunwayConfigurationDto[] Runways { get; } = runways;
    public RunwayModeConfigurationDto[] RunwayModes { get; } = runwayModes;
    public ViewConfigurationDto[] Views { get; } = views;
    public string[] FeederFixes { get; } = feederFixes;
}