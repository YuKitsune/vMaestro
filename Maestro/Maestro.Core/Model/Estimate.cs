namespace Maestro.Core.Model;

public class FixEstimate(string fixIdentifier, DateTimeOffset estimate)
{
    public string FixIdentifier { get; } = fixIdentifier;
    public DateTimeOffset Estimate { get; } = estimate;
}