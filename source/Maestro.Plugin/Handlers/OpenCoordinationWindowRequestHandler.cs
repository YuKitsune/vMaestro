using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenCoordinationWindowRequestHandler(
    WindowManager windowManager,
    IMediator mediator,
    IErrorReporter errorReporter,
    IMaestroConnectionManager connectionManager,
    IAirportConfigurationProvider airportConfigurationProvider)
    : IRequestHandler<OpenCoordinationWindowRequest>
{
    public Task Handle(OpenCoordinationWindowRequest request, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

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
                    request.Callsign is not null
                        ? airportConfiguration.FlightCoordinationMessages.Select(s => RenderMessageTemplate(s, request.Callsign)).ToArray()
                        : airportConfiguration.GlobalCoordinationMessages,
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
