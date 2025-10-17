using System.Windows;
using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;
using vatsys;

namespace Maestro.Plugin.Handlers;

public class SessionCreatedNotificationHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    IArrivalConfigurationLookup arrivalConfigurationLookup,
    WindowManager windowManager,
    IMediator mediator,
    IErrorReporter errorReporter)
    : INotificationHandler<SessionCreatedNotification>
{
    public async Task Handle(SessionCreatedNotification notification, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
            .Single(a => a.Identifier == notification.AirportIdentifier);

        var runwayModes = airportConfiguration.RunwayModes
            .Select(rm => new RunwayModeViewModel(rm))
            .ToArray();

        // TODO: EW
        var approachTypes = arrivalConfigurationLookup
            .GetArrivals()
            .Where(a => a.AirportIdentifier == notification.AirportIdentifier)
            .GroupBy(a => a.RunwayIdentifier, a => a.ApproachType)
            .ToDictionary(g => g.Key, g => g.Distinct().Where(s => !string.IsNullOrEmpty(s)).ToArray());

        windowManager.FocusOrCreateWindow(
            WindowKeys.Maestro(notification.AirportIdentifier),
            $"TFMS: {notification.AirportIdentifier}",
            _ => new MaestroView(
                new MaestroViewModel(
                    notification.AirportIdentifier,
                    airportConfiguration.Runways,
                    approachTypes,
                    runwayModes,
                    airportConfiguration.Views,
                    mediator,
                    errorReporter)),
            shrinkToContent: false,
            new Size(560, 800),
            configureForm: form =>
            {
                Plugin.AddMenuItemFor(notification.AirportIdentifier, form);

                form.Closed += async (_, _) =>
                {
                    try
                    {
                        // TODO: Revisit confirmation dialog
                        await mediator.Send(
                            new DestroySessionRequest(notification.AirportIdentifier),
                            cancellationToken);
                        Plugin.RemoveMenuItemFor(notification.AirportIdentifier);
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
            await mediator.Send(new StartSessionRequest(notification.AirportIdentifier, Network.Callsign), cancellationToken);
        }
    }
}
