using Maestro.Core.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenPendingDeparturesWindowRequestHandler(GuiInvoker guiInvoker, IMediator mediator, IClock clock, IErrorReporter errorReporter)
    : IRequestHandler<OpenPendingDeparturesWindowRequest>
{
    public Task Handle(OpenPendingDeparturesWindowRequest request, CancellationToken cancellationToken)
    {
        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var windowHandle = new WindowHandle();

            var viewModel = new PendingDeparturesViewModel(
                request.AirportIdentifier,
                request.PendingFlights,
                windowHandle,
                mediator,
                clock,
                errorReporter);

            var form = new VatSysForm(
                title: "Insert a Flight",
                new PendingDeparturesView(viewModel),
                shrinkToContent: true);

            windowHandle.SetForm(form);

            form.Show(mainForm);
        });

        return Task.CompletedTask;
    }
}
