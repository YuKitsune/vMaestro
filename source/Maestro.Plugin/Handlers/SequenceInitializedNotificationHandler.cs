using System.Windows.Forms;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class SequenceInitializedNotificationHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    GuiInvoker guiInvoker,
    IMediator mediator,
    IErrorReporter errorReporter)
    : INotificationHandler<SequenceInitializedNotification>
{
    public Task Handle(SequenceInitializedNotification notification, CancellationToken cancellationToken)
    {
        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var airportConfiguration = airportConfigurationProvider.GetAirportConfigurations()
                .Single(a => a.Identifier == notification.AirportIdentifier);

            var runwayModes = airportConfiguration.RunwayModes
                .Select(rm => new RunwayModeViewModel(rm.ToMessage()))
                .ToArray();

            var viewModel = new MaestroViewModel(
                notification.AirportIdentifier,
                runwayModes,
                new RunwayModeViewModel(notification.Sequence.CurrentRunwayMode),
                airportConfiguration.Views,
                notification.Sequence.Flights,
                notification.Sequence.Slots,
                mediator,
                errorReporter);

            var window = new VatSysForm(
                title: "TFMS",
                new MaestroView(viewModel),
                shrinkToContent: false)
            {
                Width = 560,
                Height = 800
            };

            // Wire up the FormClosing event to show confirmation dialog
            window.CustomFormClosingHandler = async (sender, e) =>
            {
                try
                {
                    // Allow closing without confirmation for system shutdowns or application exits
                    if (e.CloseReason is CloseReason.ApplicationExitCall or CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
                        return;

                    // Only show confirmation for user-initiated closes
                    if (e.CloseReason is not CloseReason.UserClosing)
                        return;

                    // Cancel the close event initially
                    e.Cancel = true;

                    // Ask for confirmation through mediator
                    var dialogMessage =
                        $"""
                         Closing the TFMS window will terminate the sequence for {notification.AirportIdentifier}.
                         Do you really want to close the window?
                         """;
                    var response = await mediator.Send(new ConfirmationRequest(dialogMessage));

                    // If user confirmed, close the window
                    if (!response.Confirmed)
                        return;

                    // Remove the event handler to prevent infinite recursion
                    window.CustomFormClosingHandler = null;
                    window.Close();

                    // Terminate the sequence
                    await mediator.Send(new StopSequencingRequest(notification.AirportIdentifier));
                }
                catch (Exception ex)
                {
                    errorReporter.ReportError(ex);
                }
            };

            window.Show(mainForm);
        });

        return Task.CompletedTask;
    }
}
