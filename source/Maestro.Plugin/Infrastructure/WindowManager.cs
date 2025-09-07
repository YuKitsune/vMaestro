using System.Windows;
using System.Windows.Forms;
using Maestro.Wpf.Integrations;

namespace Maestro.Plugin.Infrastructure;

public class WindowManager(GuiInvoker guiInvoker)
{
    readonly IDictionary<string, WindowHandle> _windows = new Dictionary<string, WindowHandle>();

    public void FocusOrCreateWindow(
        string key,
        string title,
        Func<IWindowHandle, UIElement> createView,
        bool shrinkToContent = true,
        Size? size = null,
        Action<VatSysForm>? configureForm = null)
    {
        if (_windows.TryGetValue(key, out var windowHandle))
        {
            windowHandle.Focus();
            return;
        }

        windowHandle = new WindowHandle();
        var view = createView(windowHandle);
        var form = new VatSysForm(
            title,
            view,
            shrinkToContent);
        if (size.HasValue)
        {
            form.Width = (int)size.Value.Width;
            form.Height = (int)size.Value.Height;
        }

        windowHandle.SetForm(form);

        guiInvoker.InvokeOnUiThread(mainForm => form.Show(mainForm));
        form.Closing += (_, _) => RemoveWindow(key);

        configureForm?.Invoke(form);

        _windows[key] = windowHandle;
    }

    public void AddWindow(string key, WindowHandle windowHandle)
    {
        _windows[key] = windowHandle;
    }

    public bool TryGetWindow(string key, out WindowHandle? windowHandle)
    {
        return _windows.TryGetValue(key, out windowHandle);
    }

    public bool TryGetViewModel(string key, out WindowHandle? windowHandle)
    {
        return _windows.TryGetValue(key, out windowHandle);
    }

    public void RemoveWindow(string key)
    {
        _windows.Remove(key);
    }
}
