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
    ViewModelManager viewModelManager,
    IMediator mediator,
    IErrorReporter errorReporter)
    : INotificationHandler<SequenceInitializedNotification>
{
    public async Task Handle(SequenceInitializedNotification notification, CancellationToken cancellationToken)
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

        await viewModelManager.Register(viewModel, cancellationToken);

        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var window = new VatSysForm(
                title: "TFMS",
                new MaestroView(viewModel),
                shrinkToContent: false)
            {
                Width = 560,
                Height = 800
            };

            // Wire up the FormClosing event to show confirmation dialog
            // TODO: Should this use the mediator to send a message to close the window instead?
            // E.g: StopSequencingRequest -> SequenceTerminatedNotificationHandler -> close the window
            window.CustomFormClosingHandler = async (sender, e) =>
            {
                try
                {
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
                    var response = await mediator.Send(new ConfirmationRequest("Close Maestro", dialogMessage));

                    // If user confirmed, close the window
                    if (!response.Confirmed)
                        return;

                    // Remove the event handler to prevent infinite recursion
                    window.CustomFormClosingHandler = null;
                    window.Close();

                    await viewModelManager.Unregister(notification.AirportIdentifier, CancellationToken.None);

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
    }
}
