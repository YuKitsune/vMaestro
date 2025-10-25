using System.Windows.Forms;

namespace Maestro.Plugin;

public class GuiInvoker(Action<MethodInvoker> invoker)
{
    public void InvokeOnUiThread(Action<Form> action)
    {
        var mainForm = Application.OpenForms["MainForm"];
        if (mainForm == null)
            return;

        // If already on UI thread, execute directly to avoid deadlock
        if (!mainForm.InvokeRequired)
        {
            action(mainForm);
            return;
        }

        invoker.Invoke(delegate { action(mainForm); });
    }
}