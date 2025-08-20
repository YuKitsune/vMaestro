using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenSlotWindowRequestHandler(GuiInvoker guiInvoker, IMediator mediator) : IRequestHandler<OpenSlotWindowRequest>
{
    public Task Handle(OpenSlotWindowRequest request, CancellationToken cancellationToken)
    {
        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var windowHandle = new WindowHandle();

            var viewModel = new SlotViewModel(
                request.AirportIdentifier,
                request.SlotId,
                request.StartTime,
                request.EndTime,
                request.RunwayIdentifiers,
                mediator,
                windowHandle);

            var form = new VatSysForm(
                title: "Insert Slot",
                new SlotView(viewModel),
                shrinkToContent: true);

            windowHandle.SetForm(form);

            form.Show(mainForm);
        });

        return Task.CompletedTask;
    }
}
