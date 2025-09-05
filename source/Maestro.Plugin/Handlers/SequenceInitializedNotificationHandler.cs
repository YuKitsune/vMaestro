using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
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

            window.Show(mainForm);
        });

        return Task.CompletedTask;
    }
}
