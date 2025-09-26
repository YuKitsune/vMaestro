using Maestro.Core.Configuration;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Maestro.Core.Infrastructure;

public class MaestroConnectionFactory : IMaestroConnectionFactory
{
    readonly ServerConfiguration _serverConfiguration;
    readonly IMediator _mediator;
    readonly ILogger _logger;

    public MaestroConnectionFactory(
        ServerConfiguration serverConfiguration,
        IMediator mediator,
        ILogger logger)
    {
        _serverConfiguration = serverConfiguration;
        _mediator = mediator;
        _logger = logger;
    }

    public MaestroConnection Create(string partition, string airportIdentifier, string callsign, Role role)
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(_serverConfiguration.Uri + $"?partition={partition}&airportIdentifier={airportIdentifier}&callsign={callsign}&role={role}")
            .WithServerTimeout(TimeSpan.FromSeconds(_serverConfiguration.TimeoutSeconds))
            .WithAutomaticReconnect()
            .WithStatefulReconnect()
            .AddNewtonsoftJsonProtocol()
            .Build();

        return new MaestroConnection(
            partition,
            airportIdentifier,
            _serverConfiguration,
            hubConnection,
            _mediator,
            _logger.ForContext<MaestroConnection>());
    }
}
