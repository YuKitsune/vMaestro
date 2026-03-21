using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public class RequestEnvelope
{
    [Key(0)]
    public required string OriginatingCallsign { get; init; }

    [Key(1)]
    public required string OriginatingConnectionId { get; init; }

    [Key(2)]
    public required Role OriginatingRole { get; init; }

    [Key(3)]
    public required IRelayableRequest Request { get; init; }
}
