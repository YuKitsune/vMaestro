using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenCoordinationWindowRequestHandler(
    WindowManager windowManager,
    IMediator mediator,
    IErrorReporter errorReporter,
    IMaestroConnectionManager connectionManager,
    CoordinationMessageConfiguration coordinationMessageConfiguration)
    : IRequestHandler<OpenCoordinationWindowRequest>
{
    public Task Handle(OpenCoordinationWindowRequest request, CancellationToken cancellationToken)
    {
        var generalCoordinationMessages = coordinationMessageConfiguration.Templates.Where(m => !m.Contains("{Callsign}")).ToArray();
        var flightSpecificMessages = coordinationMessageConfiguration.Templates.Where(m => m.Contains("{Callsign}")).Select(s => RenderMessageTemplate(s, request.Callsign)).ToArray();

        string[] peers = [];
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection))
        {
            peers = connection!.Peers.Select(p => p.Callsign).ToArray();
        }

        windowManager.FocusOrCreateWindow(
            WindowKeys.Coordination(request.AirportIdentifier),
            "Coordination",
            windowHandle =>
            {
                var viewModel = new CoordinationViewModel(
                    request.AirportIdentifier,
                    request.Callsign,
                    windowHandle,
                    mediator,
                    errorReporter,
                    request.Callsign is not null ? flightSpecificMessages : generalCoordinationMessages,
                    peers);

                return new CoordinationView(viewModel);
            });

        return Task.CompletedTask;
    }

    string RenderMessageTemplate(string template, string callsign)
    {
        return template.Replace("{Callsign}", callsign);
    }
}
