using Maestro.Core.Configuration;
using Maestro.Core.Sessions;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenDesequencedWindowRequestHandler(WindowManager windowManager, IMediator mediator, IErrorReporter errorReporter)
    : IRequestHandler<OpenDesequencedWindowRequest, OpenDesequencedWindowResponse>
{
    public Task<OpenDesequencedWindowResponse> Handle(OpenDesequencedWindowRequest request, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.Desequenced(request.AirportIdentifier),
            "De-sequenced",
            windowHandle => new DesequencedView(
                new DesequencedViewModel(
                    mediator,
                    windowHandle,
                    errorReporter,
                    request.AirportIdentifier,
                    request.Callsigns)));

        return Task.FromResult(new OpenDesequencedWindowResponse());
    }
}

public class OpenConnectionWindowRequestHandler(ISessionManager sessionManager, WindowManager windowManager, ServerConfiguration serverConfiguration, IMediator mediator, IErrorReporter errorReporter)
    : IRequestHandler<OpenConnectionWindowRequest>
{
    public Task Handle(OpenConnectionWindowRequest request, CancellationToken cancellationToken)
    {
        var (partition, isConnected) = GetConnectionStatus(request.AirportIdentifier, cancellationToken).GetAwaiter().GetResult();
        windowManager.FocusOrCreateWindow(
            WindowKeys.Connection(request.AirportIdentifier),
            "Setup",
            windowHandle => new ConnectionView(new ConnectionViewModel(request.AirportIdentifier, serverConfiguration, partition, isConnected, mediator, windowHandle, errorReporter)));

        return Task.CompletedTask;
    }

    async Task<(string, bool)> GetConnectionStatus(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(airportIdentifier, cancellationToken);
        return lockedSession.Session.IsConnected
            ? (lockedSession.Session.Connection?.Partition ?? string.Empty, true)
            : (string.Empty, false);
    }
}
