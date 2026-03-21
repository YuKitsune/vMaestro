using System.Diagnostics;
using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Shared;

[DebuggerDisplay("{FixIdentifier} ETA {Estimate} (ATO {ActualTimeOver})")]
[MessagePackObject]
public class FixEstimate
{
    [Key(0)]
    public string FixIdentifier { get; }

    [Key(1)]
    public DateTimeOffset Estimate { get; }

    [Key(2)]
    public DateTimeOffset? ActualTimeOver { get; }

    [JsonConstructor]
    [SerializationConstructor]
    public FixEstimate(string fixIdentifier, DateTimeOffset estimate, DateTimeOffset? actualTimeOver = null)
    {
        FixIdentifier = fixIdentifier;
        Estimate = estimate;
        ActualTimeOver = actualTimeOver;
    }
}
