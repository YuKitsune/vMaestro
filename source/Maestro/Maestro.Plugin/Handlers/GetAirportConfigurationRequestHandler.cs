using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Plugin;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class GetAirportConfigurationRequestHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    IServerConnection serverConnection)
    : IRequestHandler<GetAirportConfigurationRequest, GetAirportConfigurationResponse>
{
    public async Task<GetAirportConfigurationResponse> Handle(GetAirportConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (serverConnection.IsConnected)
        {
            return await serverConnection.SendAsync<GetAirportConfigurationRequest, GetAirportConfigurationResponse>(request, cancellationToken);
        }

        return new GetAirportConfigurationResponse(airportConfigurationProvider.GetAirportConfigurations());
    }
}
