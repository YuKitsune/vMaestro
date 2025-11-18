using MediatR;

namespace Maestro.Core.Integration;

public record GetNetworkStatusRequest : IRequest<GetNetworkStatusResponse>;
public record GetNetworkStatusResponse(bool IsConnected, string Position);
