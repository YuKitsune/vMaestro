namespace TFMS.Core.Dtos.Configuration;

public class RunwayConfigurationDTO(string identifier, int defaultLandingRateSeconds)
{
    public string Identifier { get; } = identifier;
    public int DefaultLandingRateSeconds { get; } = defaultLandingRateSeconds;
}
