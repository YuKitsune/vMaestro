using MediatR;
using TFMS.Core.Configuration;
using TFMS.Core.Dtos.Messages;

namespace TFMS.Plugin.Handlers;

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
