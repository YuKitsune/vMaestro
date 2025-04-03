namespace Maestro.Core.Dtos.Configuration;

public class ViewConfigurationDTO(string identifier, LadderConfigurationDTO? leftLadderConfiguration, LadderConfigurationDTO? rightLadderConfiguration, LadderReferenceTime ladderReferenceTime)
{
    public string Identifier { get; } = identifier;
    public LadderConfigurationDTO? LeftLadderConfiguration { get; } = leftLadderConfiguration;
    public LadderConfigurationDTO? RightLadderConfiguration { get; } = rightLadderConfiguration;
    public LadderReferenceTime LadderReferenceTime { get; } = ladderReferenceTime;
}
