using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public class RequestEnvelope
{
    public required string OriginatingCallsign { get; init; }
    public required string OriginatingConnectionId { get; init; }
    public required Role OriginatingRole { get; init; }
    public required IRequest Request { get; init; }
}

