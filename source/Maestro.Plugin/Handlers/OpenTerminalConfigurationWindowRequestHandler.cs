using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using Maestro.Plugin.Infrastructure;
using Maestro.Avalonia.Contracts;
using Maestro.Avalonia.Integrations;
using Maestro.Avalonia.ViewModels;
using Maestro.Avalonia.Views;
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
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);

        var sessionDto = session.Snapshot();

        var runwayModes = airportConfiguration.RunwayModes
            .Select(r => new RunwayModeViewModel(r))
            .ToArray();

        windowManager.FocusOrCreateWindow(
            WindowKeys.TerminalConfiguration(request.AirportIdentifier),
            "TMA Configuration",
            windowHandle =>
            {
                var lastLandingTime = sessionDto.Sequence.LastLandingTimeForCurrentMode == default
                    ? clock.UtcNow()
                    : sessionDto.Sequence.LastLandingTimeForCurrentMode;

                var firstLandingTime = sessionDto.Sequence.FirstLandingTimeForNextMode == default
                    ? clock.UtcNow()
                    : lastLandingTime.AddMinutes(5);

                var viewModel = new TerminalConfigurationViewModel(
                    request.AirportIdentifier,
                    runwayModes,
                    new RunwayModeViewModel(sessionDto.Sequence.CurrentRunwayMode),
                    sessionDto.Sequence.NextRunwayMode is not null ? new RunwayModeViewModel(sessionDto.Sequence.NextRunwayMode) : null,
                    lastLandingTime,
                    firstLandingTime,
                    airportConfiguration,
                    sessionDto.Sequence.SurfaceWind,
                    mediator,
                    windowHandle,
                    errorReporter);

                return new TerminalConfigurationView(viewModel);
            });
    }
}
