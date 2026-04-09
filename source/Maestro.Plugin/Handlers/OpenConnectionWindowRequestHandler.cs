using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Plugin.Infrastructure;
using Maestro.Avalonia.Contracts;
using Maestro.Avalonia.Integrations;
using Maestro.Avalonia.ViewModels;
using Maestro.Avalonia.Views;
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
        var (partition, isConnected, isReady) = GetConnectionStatus(request.AirportIdentifier);
        windowManager.FocusOrCreateWindow(
            WindowKeys.Connection(request.AirportIdentifier),
            "Setup",
            windowHandle => new ConnectionView(new ConnectionViewModel(request.AirportIdentifier, serverConfiguration, partition, isConnected, isReady, mediator, windowHandle, errorReporter)));

        return Task.CompletedTask;
    }

    (string, bool, bool) GetConnectionStatus(string airportIdentifier)
    {
        var connectionExists = connectionManager.TryGetConnection(airportIdentifier, out var connection);
        var isConnected = connectionExists && connection is not null && connection.IsConnected;
        var partition = connection?.Partition ?? string.Empty;

        return (partition, isConnected, connectionExists);
    }
}
