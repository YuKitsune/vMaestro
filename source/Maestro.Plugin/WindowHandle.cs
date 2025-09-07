using System.Windows.Forms;
using Maestro.Wpf.Integrations;

namespace Maestro.Plugin;

public class WindowHandle : IWindowHandle
{
    Form? _form;

    public object? ViewModel { get; private set; }

    public void Focus()
    {
        if (_form is null)
            return;

        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    public void Close()
    {
        _form?.Close();
    }

    public void SetForm(Form form)
    {
        _form = form;
    }

    public void SetViewModel(object viewModel)
    {
        ViewModel = viewModel;
    }
}
