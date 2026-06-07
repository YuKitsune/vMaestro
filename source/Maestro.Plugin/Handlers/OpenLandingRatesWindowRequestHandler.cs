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

public class OpenLandingRatesWindowRequestHandler(
    WindowManager windowManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    ISessionManager sessionManager,
    IMediator mediator,
    IClock clock,
    IErrorReporter errorReporter)
    : IRequestHandler<OpenLandingRatesRequest>
{
    public async Task Handle(OpenLandingRatesRequest request, CancellationToken cancellationToken)
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
            WindowKeys.LandingRates(request.AirportIdentifier),
            "Landing Rates",
            windowHandle =>
            {
                var pendingRatesChange = sessionDto.Sequence.PendingConfigurationChange as LandingRatesChangeDto;

                var changeTime = pendingRatesChange is null
                    ? clock.UtcNow()
                    : pendingRatesChange.ChangeTime;

                var viewModel = new LandingRatesViewModel(
                    request.AirportIdentifier,
                    runwayModes,
                    new RunwayModeViewModel(sessionDto.Sequence.CurrentRunwayMode),
                    pendingRatesChange is not null ? new RunwayModeViewModel(sessionDto.Sequence.CurrentRunwayMode) : null,
                    changeTime,
                    airportConfiguration,
                    sessionDto.Sequence.SurfaceWind,
                    mediator,
                    windowHandle,
                    clock,
                    errorReporter);

                return new LandingRatesView(viewModel);
            });
    }
}
