using Maestro.Core.Model;

namespace Maestro.Core.Dtos.Configuration;

public class AirportConfigurationDto(string identifier, RunwayConfigurationDto[] runways, RunwayModeConfigurationDto[] runwayModes, ViewConfigurationDto[] views, FixConfigurationDto[] feederFixes)
{
    public string Identifier { get; } = identifier;
    public RunwayConfigurationDto[] Runways { get; } = runways;
    public RunwayModeConfigurationDto[] RunwayModes { get; } = runwayModes;
    public ViewConfigurationDto[] Views { get; } = views;
    public FixConfigurationDto[] FeederFixes { get; } = feederFixes;
}

public class FixConfigurationDto
{
    public required string Identifier { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
}
