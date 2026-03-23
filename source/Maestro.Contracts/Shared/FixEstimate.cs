using System.Diagnostics;
using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Shared;

[DebuggerDisplay("{FixIdentifier} ETA {Estimate}")]
[MessagePackObject]
public class FixEstimate
{
    [Key(0)]
    public string FixIdentifier { get; }

    [Key(1)]
    public DateTimeOffset? Estimate { get; }

    [JsonConstructor]
    [SerializationConstructor]
    public FixEstimate(string fixIdentifier, DateTimeOffset? estimate)
    {
        FixIdentifier = fixIdentifier;
        Estimate = estimate;
    }
}
