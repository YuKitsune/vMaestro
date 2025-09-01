using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenInsertFlightWindowRequestHandler(GuiInvoker guiInvoker, IMediator mediator, IErrorReporter errorReporter)
    : IRequestHandler<OpenInsertFlightWindowRequest>
{
    public Task Handle(OpenInsertFlightWindowRequest request, CancellationToken cancellationToken)
    {
        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var windowHandle = new WindowHandle();

            var viewModel = new InsertFlightViewModel(
                request.AirportIdentifier,
                request.Options,
                request.LandedFlights,
                request.PendingFlights,
                windowHandle,
                mediator,
                errorReporter);

            var form = new VatSysForm(
                title: "Insert a Flight",
                new InsertFlightView(viewModel),
                shrinkToContent: true);

            windowHandle.SetForm(form);

            form.Show(mainForm);
        });

        return Task.CompletedTask;
    }
}
