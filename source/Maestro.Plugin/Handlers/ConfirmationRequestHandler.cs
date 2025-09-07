using System.Windows.Forms;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class ConfirmationRequestHandler(GuiInvoker guiInvoker) : IRequestHandler<ConfirmationRequest, ConfirmationResponse>
{
    public Task<ConfirmationResponse> Handle(ConfirmationRequest request, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<ConfirmationResponse>();

        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            try
            {
                VatSysForm dialogForm = null!;

                var dialogViewModel = new DialogViewModel(
                    request.Message,
                    result =>
                    {
                        if (!taskCompletionSource.Task.IsCompleted)
                        {
                            taskCompletionSource.SetResult(new ConfirmationResponse(result));
                        }
                    },
                    closeDialog: () => dialogForm?.Close());

                var dialogView = new DialogView(dialogViewModel);

                dialogForm = new VatSysForm(request.Title, dialogView, shrinkToContent: true);

                // Handle form closing via X button as cancel
                dialogForm.FormClosing += (sender, e) =>
                {
                    if (!taskCompletionSource.Task.IsCompleted)
                    {
                        taskCompletionSource.SetResult(new ConfirmationResponse(false));
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
