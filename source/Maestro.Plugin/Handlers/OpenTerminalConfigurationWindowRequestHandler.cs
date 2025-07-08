using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenTerminalConfigurationWindowRequestHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    ISequenceProvider sequenceProvider,
    GuiInvoker guiInvoker,
    IMediator mediator,
    IClock clock)
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

        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var windowHandle = new WindowHandle();
            var viewModel = new TerminalConfigurationViewModel(
                request.AirportIdentifier,
                runwayModes,
                sequence.CurrentRunwayMode,
                sequence.NextRunwayMode,
                sequence.RunwayModeChangeTime,
                mediator,
                windowHandle,
                clock);

            var form = new VatSysForm(
                title: "TMA Configuration",
                new TerminalConfigurationView(viewModel),
                shrinkToContent: false);

            windowHandle.SetForm(form);

            form.Show(mainForm);
        });

        return Task.CompletedTask;
    }
}
