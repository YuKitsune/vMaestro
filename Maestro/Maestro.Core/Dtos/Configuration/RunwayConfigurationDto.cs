namespace Maestro.Core.Dtos.Configuration;

public class RunwayConfigurationDto(string identifier, int defaultLandingRateSeconds, LadderPosition ladderPosition)
{
    public string Identifier { get; } = identifier;
    public int DefaultLandingRateSeconds { get; } = defaultLandingRateSeconds;
    public LadderPosition LadderPosition { get; } = ladderPosition;
}
