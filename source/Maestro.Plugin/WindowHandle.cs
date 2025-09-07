using System.Windows.Forms;
using Maestro.Wpf.Integrations;

namespace Maestro.Plugin;

public class WindowHandle(VatSysForm form) : IWindowHandle
{
    public void Focus()
    {
        form.WindowState = FormWindowState.Normal;
        form.Activate();
    }

    public void Close()
    {
        form.ForceClose();
    }
}
