using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
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
    ISequenceProvider sequenceProvider,
    IMessageDispatcher messageDispatcher,
    IClock clock,
    IErrorReporter errorReporter)
    : IRequestHandler<OpenTerminalConfigurationRequest>
{
    public Task Handle(OpenTerminalConfigurationRequest request, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
            .Single(a => a.Identifier == request.AirportIdentifier);
        var sequence = sequenceProvider.GetReadOnlySequence(request.AirportIdentifier);
        var runwayModes = airportConfiguration.RunwayModes
            .Select(r => r.ToMessage())
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
                    sequence.CurrentRunwayMode,
                    sequence.NextRunwayMode,
                    lastLandingTime,
                    firstLandingTime,
                    messageDispatcher,
                    windowHandle,
                    clock,
                    errorReporter);

                return new TerminalConfigurationView(viewModel);
            });

        return Task.CompletedTask;
    }
}
