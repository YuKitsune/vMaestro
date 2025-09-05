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
                    // Cancel the close event initially
                    e.Cancel = true;

                    // Ask for confirmation through mediator
                    var response = await mediator.Send(new WindowCloseConfirmationRequest(notification.AirportIdentifier));

                    // If user confirmed, close the window
                    if (response.AllowClose)
                    {
                        // Remove the event handler to prevent infinite recursion
                        window.CustomFormClosingHandler = null;
                        window.Close();
                    }
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
