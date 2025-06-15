namespace Maestro.Core.Configuration;

public class ViewConfiguration
{
    public required string Identifier { get; init; }
    public required ViewMode ViewMode { get; init; }
    public required string[] LeftLadder { get; init; }
    
    public required string[] RightLadder { get; init; }
}
