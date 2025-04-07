using System.Windows.Forms;

namespace Maestro.Plugin;

public class GuiInvoker(Action<MethodInvoker> invoker)
{
    public void InvokeOnUiThread(Action<Form> action)
    {
        var mainForm = Application.OpenForms["MainForm"];
        if (mainForm == null)
            return;
        
        invoker.Invoke(delegate { action(mainForm); });
    }
}