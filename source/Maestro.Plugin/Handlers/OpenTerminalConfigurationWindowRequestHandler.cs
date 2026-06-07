using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
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
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);

        SessionDto sessionDto;
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            sessionDto = session.Snapshot();
        }

        var runwayModes = airportConfiguration.RunwayModes
            .Select(r => new RunwayModeViewModel(r, airportConfiguration.DefaultOffModeSeparationSeconds))
            .ToArray();

        windowManager.FocusOrCreateWindow(
            WindowKeys.TerminalConfiguration(request.AirportIdentifier),
            "TMA Configuration",
            windowHandle =>
            {
                var pendingModeChange = sessionDto.Sequence.PendingConfigurationChange as TerminalConfigurationChangeDto;

                var lastLandingTime = pendingModeChange is null
                    ? clock.UtcNow()
                    : pendingModeChange.LastLandingTimeInPreviousMode;

                var firstLandingTime = pendingModeChange is null
                    ? clock.UtcNow()
                    : pendingModeChange.FirstLandingTimeInNewMode;

                var viewModel = new TerminalConfigurationViewModel(
                    request.AirportIdentifier,
                    runwayModes,
                    new RunwayModeViewModel(sessionDto.Sequence.CurrentRunwayMode),
                    pendingModeChange is not null ? new RunwayModeViewModel(pendingModeChange.NewRunwayMode) : null,
                    lastLandingTime,
                    firstLandingTime,
                    airportConfiguration,
                    sessionDto.Sequence.SurfaceWind,
                    mediator,
                    windowHandle,
                    clock,
                    errorReporter);

                return new TerminalConfigurationView(viewModel);
            });
    }
}
