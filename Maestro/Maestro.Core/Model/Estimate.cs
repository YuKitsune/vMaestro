using System.Diagnostics;

namespace Maestro.Core.Model;

[DebuggerDisplay("{FixIdentifier} {Estimate}")]
public class FixEstimate(string fixIdentifier, DateTimeOffset estimate)
{
    public string FixIdentifier { get; } = fixIdentifier;
    public DateTimeOffset Estimate { get; } = estimate;
}