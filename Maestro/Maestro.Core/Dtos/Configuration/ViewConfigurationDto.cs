namespace Maestro.Core.Dtos.Configuration;

public class ViewConfigurationDto(string identifier, LadderConfigurationDto? leftLadderConfiguration, LadderConfigurationDto? rightLadderConfiguration, LadderReferenceTime ladderReferenceTime)
{
    public string Identifier { get; } = identifier;
    public LadderConfigurationDto? LeftLadderConfiguration { get; } = leftLadderConfiguration;
    public LadderConfigurationDto? RightLadderConfiguration { get; } = rightLadderConfiguration;
    public LadderReferenceTime LadderReferenceTime { get; } = ladderReferenceTime;
}
