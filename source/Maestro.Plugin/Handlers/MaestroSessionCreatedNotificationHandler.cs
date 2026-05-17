using System.Windows;
using Maestro.Core.Configuration;
using Maestro.Core.Sessions.Contracts;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;
using Serilog;
using vatsys;

namespace Maestro.Plugin.Handlers;

public class MaestroSessionCreatedNotificationHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    LabelsConfiguration labelsConfiguration,
    WindowManager windowManager,
    IMediator mediator,
    IErrorReporter errorReporter,
    ILogger logger)
    : INotificationHandler<MaestroSessionCreatedNotification>
{
    public async Task Handle(MaestroSessionCreatedNotification notification, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(notification.AirportIdentifier);

        var runwayModes = airportConfiguration.RunwayModes
            .Select(rm => new RunwayModeViewModel(rm, airportConfiguration.DefaultOffModeSeparationSeconds))
            .ToArray();

        windowManager.FocusOrCreateWindow(
            WindowKeys.Maestro(notification.AirportIdentifier),
            $"TFMS: {notification.AirportIdentifier}",
            _ => new MaestroView(
                new MaestroViewModel(
                    notification.AirportIdentifier,
                    airportConfiguration.Runways, // TODO: Remove these, and just use the Airport Configuration
                    runwayModes,
                    airportConfiguration.Views,
                    mediator,
                    errorReporter,
                    labelsConfiguration,
                    airportConfiguration),
                logger),
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
                            new DestroyMaestroSessionRequest(notification.AirportIdentifier),
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
