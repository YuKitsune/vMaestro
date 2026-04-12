using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenConnectionWindowRequestHandler(
    IMaestroConnectionManager connectionManager,
    WindowManager windowManager,
    ServerConfiguration serverConfiguration,
    IMediator mediator,
    IErrorReporter errorReporter)
    : IRequestHandler<OpenConnectionWindowRequest>
{
    public Task Handle(OpenConnectionWindowRequest request, CancellationToken cancellationToken)
    {
        var (environment, isConnected, isReady) = GetConnectionStatus(request.AirportIdentifier);
        windowManager.FocusOrCreateWindow(
            WindowKeys.Connection(request.AirportIdentifier),
            "Setup",
            windowHandle => new ConnectionView(new ConnectionViewModel(request.AirportIdentifier, serverConfiguration, environment, isConnected, isReady, mediator, windowHandle, errorReporter)));

        return Task.CompletedTask;
    }

    (string, bool, bool) GetConnectionStatus(string airportIdentifier)
    {
        var connectionExists = connectionManager.TryGetConnection(airportIdentifier, out var connection);
        var isConnected = connectionExists && connection is not null && connection.IsConnected;
        var environment = connection?.Environment ?? string.Empty;

        return (environment, isConnected, connectionExists);
    }
}
