using System.Diagnostics;

namespace Maestro.Core.Model;

[DebuggerDisplay("{FixIdentifier} ETA {Estimate} (ATO {ActualTimeOver})")]
public class FixEstimate(string fixIdentifier, DateTimeOffset estimate, DateTimeOffset? actualTimeOver = null)
{
    public string FixIdentifier { get; } = fixIdentifier;
    public DateTimeOffset Estimate { get; } = estimate;
    public DateTimeOffset? ActualTimeOver { get; } = actualTimeOver;
}