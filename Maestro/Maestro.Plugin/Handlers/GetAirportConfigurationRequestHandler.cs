using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Messages;
using Maestro.Plugin;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class GetAirportConfigurationRequestHandler(IAirportConfigurationProvider airportConfigurationProvider, IServerConnection serverConnection)
    : IRequestHandler<GetAirportConfigurationRequest, GetAirportConfigurationResponse>
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider = airportConfigurationProvider;
    readonly IServerConnection _serverConnection = serverConnection;

    public async Task<GetAirportConfigurationResponse> Handle(GetAirportConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (_serverConnection.IsConnected)
        {
            return await _serverConnection.SendAsync<GetAirportConfigurationRequest, GetAirportConfigurationResponse>(request, cancellationToken);
        }

        return new GetAirportConfigurationResponse(_airportConfigurationProvider.GetAirportConfigurations());
    }
}
