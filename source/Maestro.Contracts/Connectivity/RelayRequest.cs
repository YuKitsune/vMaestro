using MediatR;
using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public class RelayRequest : IRequest<ServerResponse>
{
    [Key(0)]
    public required RequestEnvelope Envelope { get; init; }

    [Key(1)]
    public required string ActionKey { get; init; }
}
