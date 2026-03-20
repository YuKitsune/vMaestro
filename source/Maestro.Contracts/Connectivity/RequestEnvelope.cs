namespace Maestro.Contracts.Connectivity;

public class RequestEnvelope
{
    public required string OriginatingCallsign { get; init; }
    public required string OriginatingConnectionId { get; init; }
    public required Role OriginatingRole { get; init; }
    public required IRelayableRequest Request { get; init; }
}
