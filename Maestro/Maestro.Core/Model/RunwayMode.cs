namespace Maestro.Core.Model;

public class RunwayMode
{
    public required string Identifier { get; init; }
    
    public required Dictionary<string, TimeSpan> LandingRates { get; init; }
}