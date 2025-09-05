using System.Windows.Forms;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class WindowCloseConfirmationRequestHandler(GuiInvoker guiInvoker)
    : IRequestHandler<WindowCloseConfirmationRequest, WindowCloseConfirmationResponse>
{
    public Task<WindowCloseConfirmationResponse> Handle(
        WindowCloseConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<WindowCloseConfirmationResponse>();

        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            try
            {
                var dialogMessage =
                    $"""
                     Closing the TFMS window will terminate the sequence for {request.AirportIdentifier}.
                     Do you really want to close the window?
                     """;

                VatSysForm dialogForm = null!;

                var dialogViewModel = new DialogViewModel(
                    dialogMessage,
                    result =>
                    {
                        if (!taskCompletionSource.Task.IsCompleted)
                        {
                            taskCompletionSource.SetResult(new WindowCloseConfirmationResponse(result));
                        }
                    },
                    closeDialog: () => dialogForm?.Close());

                var dialogView = new DialogView(dialogViewModel);

                dialogForm = new VatSysForm(dialogView, shrinkToContent: true);

                // Handle form closing via X button as cancel
                dialogForm.FormClosing += (sender, e) =>
                {
                    if (!taskCompletionSource.Task.IsCompleted)
                    {
                        taskCompletionSource.SetResult(new WindowCloseConfirmationResponse(false));
                    }
                };

                dialogForm.ShowDialog(mainForm);
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }
        });

        return taskCompletionSource.Task;
    }
}
