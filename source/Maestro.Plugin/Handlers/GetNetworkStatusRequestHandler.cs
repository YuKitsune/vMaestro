using Maestro.Core.Integration;
using MediatR;
using vatsys;

namespace Maestro.Plugin.Handlers;


public class GetNetworkStatusRequestHandler : IRequestHandler<GetNetworkStatusRequest, GetNetworkStatusResponse>
{
    public Task<GetNetworkStatusResponse> Handle(GetNetworkStatusRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new GetNetworkStatusResponse(Network.IsConnected, Network.Callsign));
    }
}
