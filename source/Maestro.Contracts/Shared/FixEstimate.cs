using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Maestro.Contracts.Shared;

[DebuggerDisplay("{FixIdentifier} ETA {Estimate} (ATO {ActualTimeOver})")]
public class FixEstimate
{
    public string FixIdentifier { get; }
    public DateTimeOffset Estimate { get; }
    public DateTimeOffset? ActualTimeOver { get; }

    [JsonConstructor]
    public FixEstimate(string fixIdentifier, DateTimeOffset estimate, DateTimeOffset? actualTimeOver = null)
    {
        FixIdentifier = fixIdentifier;
        Estimate = estimate;
        ActualTimeOver = actualTimeOver;
    }
}
