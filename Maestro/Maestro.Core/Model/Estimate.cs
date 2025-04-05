namespace Maestro.Core.Model;

public class FixEstimate
{
    public required string FixIdentifier { get; init; }
    public required DateTimeOffset Estimate { get; init; }
}