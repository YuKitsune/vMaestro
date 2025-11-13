using Maestro.Core.Configuration;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenTerminalConfigurationWindowRequestHandler(
    WindowManager windowManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IMaestroInstanceManager instanceManager,
    IMediator mediator,
    IClock clock,
    IErrorReporter errorReporter)
    : IRequestHandler<OpenTerminalConfigurationRequest>
{
    public async Task Handle(OpenTerminalConfigurationRequest request, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
            .Single(a => a.Identifier == request.AirportIdentifier);

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        var sequenceMessage = instance.Session.Sequence.ToMessage();

        var runwayModes = airportConfiguration.RunwayModes
            .Select(r => new RunwayModeViewModel(r))
            .ToArray();

        windowManager.FocusOrCreateWindow(
            WindowKeys.TerminalConfiguration(request.AirportIdentifier),
            "TMA Configuration",
            windowHandle =>
            {
                var lastLandingTime = sequenceMessage.LastLandingTimeForCurrentMode == default
                    ? clock.UtcNow()
                    : sequenceMessage.LastLandingTimeForCurrentMode;

                var firstLandingTime = sequenceMessage.FirstLandingTimeForNextMode == default
                    ? clock.UtcNow()
                    : lastLandingTime.AddMinutes(5); // Make configurable

                var viewModel = new TerminalConfigurationViewModel(
                    request.AirportIdentifier,
                    runwayModes,
                    new RunwayModeViewModel(sequenceMessage.CurrentRunwayMode),
                    sequenceMessage.NextRunwayMode is not null ? new RunwayModeViewModel(sequenceMessage.NextRunwayMode) : null,
                    lastLandingTime,
                    firstLandingTime,
                    mediator,
                    windowHandle,
                    clock,
                    errorReporter);

                return new TerminalConfigurationView(viewModel);
            });
    }
}
