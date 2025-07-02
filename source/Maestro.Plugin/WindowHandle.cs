using System.Windows.Forms;
using Maestro.Wpf.Integrations;

namespace Maestro.Plugin;

public class WindowHandle : IWindowHandle
{
    Form? _form;
    
    public void Close()
    {
        _form?.Close();
    }

    public void SetForm(Form form)
    {
        _form = form;
    }
}