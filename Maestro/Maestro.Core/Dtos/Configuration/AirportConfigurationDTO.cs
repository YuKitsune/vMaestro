namespace Maestro.Core.Dtos.Configuration;

public class AirportConfigurationDTO(string identifier, RunwayConfigurationDTO[] runways, RunwayModeConfigurationDTO[] runwayModes, SectorConfigurationDTO[] sectors, string[] feederFixes)
{
    public string Identifier { get; } = identifier;
    public RunwayConfigurationDTO[] Runways { get; } = runways;
    public RunwayModeConfigurationDTO[] RunwayModes { get; } = runwayModes;
    public SectorConfigurationDTO[] Sectors { get; } = sectors;
    public string[] FeederFixes { get; } = feederFixes;
}
