using System.Windows;
using Maestro.Core.Configuration;
using Maestro.Core.Hosting;
using Maestro.Core.Hosting.Contracts;
using Maestro.Core.Messages;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;
using vatsys;

namespace Maestro.Plugin.Handlers;

public class MaestroInstanceCreatedNotificationHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    WindowManager windowManager,
    IMediator mediator,
    IErrorReporter errorReporter)
    : INotificationHandler<MaestroInstanceCreatedNotification>
{
    public async Task Handle(MaestroInstanceCreatedNotification notification, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
            .Single(a => a.Identifier == notification.AirportIdentifier);

        var runwayModes = airportConfiguration.RunwayModes
            .Select(rm => new RunwayModeViewModel(rm))
            .ToArray();

        windowManager.FocusOrCreateWindow(
            WindowKeys.Maestro(notification.AirportIdentifier),
            $"TFMS: {notification.AirportIdentifier}",
            _ => new MaestroView(
                new MaestroViewModel(
                    notification.AirportIdentifier,
                    airportConfiguration.Runways.Select(r => r.Identifier).ToArray(),
                    runwayModes,
                    airportConfiguration.Views,
                    mediator,
                    errorReporter)),
            shrinkToContent: false,
            new Size(640, 800),
            configureForm: form =>
            {
                form.Closed += async (_, _) =>
                {
                    try
                    {
                        // TODO: Revisit confirmation dialog
                        await mediator.Send(
                            new DestroyMaestroInstanceRequest(notification.AirportIdentifier),
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        errorReporter.ReportError(ex);
                    }
                };
            });

        // Immediately start session if we're connected to VATSIM
        if (Network.IsConnected)
        {
            await mediator.Publish(new NetworkConnectedNotification(Network.Callsign), cancellationToken);
        }
    }
}
