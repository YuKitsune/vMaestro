using System.Windows;
using System.Windows.Forms;
using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;
using vatsys;

namespace Maestro.Plugin.Handlers;

public class SessionCreatedNotificationHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    WindowManager windowManager,
    IMediator mediator,
    IErrorReporter errorReporter,
    INotificationStream<SequenceUpdatedNotification> sequenceSequenceUpdatedNotification)
    : INotificationHandler<SessionCreatedNotification>
{
    public async Task Handle(SessionCreatedNotification notification, CancellationToken cancellationToken)
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
                    errorReporter,
                    sequenceSequenceUpdatedNotification)),
            shrinkToContent: false,
            new Size(560, 800),
            configureForm: form =>
            {
                Plugin.AddMenuItemFor(notification.AirportIdentifier, form);

                // TODO: Can we handle this in some separate place?
                form.CustomFormClosing += async (_, e) =>
                {
                    try
                    {
                        // Only show confirmation for user-initiated closes
                        // Application exits and programmatic closes should not be blocked
                        if (e.CloseReason is not CloseReason.UserClosing)
                            return;

                        e.Cancel = true;

                        // Ask for confirmation through mediator
                        var dialogMessage =
                            $"""
                             Closing the TFMS window will terminate the sequence for {notification.AirportIdentifier}.
                             Do you really want to close the window?
                             """;
                        var response = await mediator.Send(new ConfirmationRequest("Close Maestro", dialogMessage),
                            cancellationToken);

                        // If user confirmed, terminate the session
                        if (!response.Confirmed)
                            return;

                        await mediator.Send(
                            new DestroySessionRequest(notification.AirportIdentifier),
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        errorReporter.ReportError(ex);
                    }
                };

                form.Closed += (_, _) => Plugin.RemoveMenuItemFor(notification.AirportIdentifier);
            });

        // Immediately start session if we're connected to VATSIM
        if (Network.IsConnected)
        {
            await mediator.Send(new StartSessionRequest(notification.AirportIdentifier, Network.Callsign), cancellationToken);
        }
    }
}
