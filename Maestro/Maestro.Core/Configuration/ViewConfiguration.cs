namespace Maestro.Core.Configuration;

public class ViewConfiguration
{
    public required string Identifier { get; init; }
    public required LadderReferenceTime LadderReferenceTime { get; init; }
    public required LadderConfiguration LeftLadderConfiguration { get; init; }
    public required LadderConfiguration RightLadderConfiguration { get; init; }
}
