using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenChangeFeederFixEstimateWindowRequestHandler(GuiInvoker guiInvoker, IMediator mediator, IErrorReporter errorReporter)
    : IRequestHandler<OpenChangeFeederFixEstimateWindowRequest>
{
    public Task Handle(OpenChangeFeederFixEstimateWindowRequest request, CancellationToken cancellationToken)
    {
        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var windowHandle = new WindowHandle();

            var viewModel = new ChangeFeederFixEstimateViewModel(
                request.AirportIdentifier,
                request.Callsign,
                request.FeederFix,
                request.OriginalFeederFixEstimate,
                windowHandle,
                mediator,
                errorReporter);

            var form = new VatSysForm(
                title: "Change ETA_FF",
                new ChangeFeederFixEstimateView(viewModel),
                shrinkToContent: true);

            windowHandle.SetForm(form);

            form.Show(mainForm);
        });

        return Task.CompletedTask;
    }
}
