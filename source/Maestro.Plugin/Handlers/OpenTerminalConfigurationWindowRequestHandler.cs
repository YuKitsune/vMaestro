using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
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
    ISessionManager sessionManager,
    IMediator mediator,
    IClock clock,
    IErrorReporter errorReporter)
    : IRequestHandler<OpenTerminalConfigurationRequest>
{
    public async Task Handle(OpenTerminalConfigurationRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);

        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
            .Single(a => a.Identifier == request.AirportIdentifier);

        var sequence = lockedSession.Session.Sequence;
        var runwayModes = airportConfiguration.RunwayModes
            .Select(r => new RunwayModeViewModel(r))
            .ToArray();

        windowManager.FocusOrCreateWindow(
            WindowKeys.TerminalConfiguration(request.AirportIdentifier),
            "TMA Configuration",
            windowHandle =>
            {
                var lastLandingTime = sequence.LastLandingTimeForCurrentMode == default
                    ? clock.UtcNow()
                    : sequence.LastLandingTimeForCurrentMode;

                var firstLandingTime = sequence.FirstLandingTimeForNextMode == default
                    ? clock.UtcNow()
                    : lastLandingTime.AddMinutes(5); // Make configurable

                var viewModel = new TerminalConfigurationViewModel(
                    request.AirportIdentifier,
                    runwayModes,
                    new RunwayModeViewModel(sequence.CurrentRunwayMode.ToMessage()),
                    sequence.NextRunwayMode is not null ? new RunwayModeViewModel(sequence.NextRunwayMode.ToMessage()) : null,
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
