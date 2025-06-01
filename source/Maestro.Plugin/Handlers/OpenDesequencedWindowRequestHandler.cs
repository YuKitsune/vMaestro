using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenDesequencedWindowRequestHandler(GuiInvoker guiInvoker, IMediator mediator) : IRequestHandler<OpenDesequencedWindowRequest, OpenDesequencedWindowResponse>
{
    public Task<OpenDesequencedWindowResponse> Handle(OpenDesequencedWindowRequest request, CancellationToken cancellationToken)
    {
        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            var child = new DesequencedView(new DesequencedViewModel(mediator, request.AirportIdentifier, request.Callsigns));
            var form = new VatSysForm(
                title: "De-sequenced",
                child,
                shrinkToContent: true);
            
            form.Show(mainForm);
        });
        
        return Task.FromResult(new OpenDesequencedWindowResponse());
    }
}