using MediatR;

namespace Maestro.Contracts.Connectivity;

public class RelayRequest : IRequest<ServerResponse>
{
    public required RequestEnvelope Envelope { get; init; }
    public required string ActionKey { get; init; }
}
