using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class ConfirmationRequestHandler(GuiInvoker guiInvoker) : IRequestHandler<ConfirmationRequest, ConfirmationResponse>
{
    public Task<ConfirmationResponse> Handle(ConfirmationRequest request, CancellationToken cancellationToken)
    {
        // TODO: Open the dialog view blocking input to all other windows until closed
        // If the user confirms, return ConfirmationResponse(true), otherwise ConfirmationResponse(false)
        throw new NotImplementedException();
    }
}
