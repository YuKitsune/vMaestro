namespace Maestro.Core.Dtos.Configuration;

public class SectorConfigurationDTO(string identifier, string[] fixes)
{
    public string Identifier { get; } = identifier;
    public string[] Fixes { get; } = fixes;
}
