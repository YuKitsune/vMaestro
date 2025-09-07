using System.Windows;
using System.Windows.Forms;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class SequenceInitializedNotificationHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    WindowManager windowManager,
    IMediator mediator,
    IErrorReporter errorReporter,
    INotificationStream<SequenceUpdatedNotification> sequenceSequenceUpdatedNotification)
    : INotificationHandler<SequenceInitializedNotification>
{
    public Task Handle(SequenceInitializedNotification notification, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
            .Single(a => a.Identifier == notification.AirportIdentifier);

        var runwayModes = airportConfiguration.RunwayModes
            .Select(rm => new RunwayModeViewModel(rm.ToMessage()))
            .ToArray();

        windowManager.FocusOrCreateWindow(
            WindowKeys.Maestro(notification.AirportIdentifier),
            $"TFMS: {notification.AirportIdentifier}",
            _ => new MaestroView(new MaestroViewModel(
                notification.AirportIdentifier,
                runwayModes,
                new RunwayModeViewModel(notification.Sequence.CurrentRunwayMode),
                airportConfiguration.Views,
                notification.Sequence.Flights,
                notification.Sequence.Slots,
                mediator,
                errorReporter,
                sequenceSequenceUpdatedNotification)),
            shrinkToContent: false,
            new Size(560, 800),
            configureForm: form =>
            {
                Plugin.AddMenuItemFor(notification.AirportIdentifier, form);

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
                        var response = await mediator.Send(new ConfirmationRequest("Close Maestro", dialogMessage));

                        // If user confirmed, terminate the sequence
                        if (!response.Confirmed)
                            return;

                        // Set flag and close programmatically to avoid re-triggering confirmation
                        await mediator.Send(new StopSequencingRequest(notification.AirportIdentifier));
                    }
                    catch (Exception ex)
                    {
                        errorReporter.ReportError(ex);
                    }
                };

                form.Closed += (_, _) => Plugin.RemoveMenuItemFor(notification.AirportIdentifier);
            });

        return Task.CompletedTask;
    }
}
