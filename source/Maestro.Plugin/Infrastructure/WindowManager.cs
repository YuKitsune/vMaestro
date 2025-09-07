using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Maestro.Wpf.Integrations;

namespace Maestro.Plugin.Infrastructure;

public class WindowManager(GuiInvoker guiInvoker)
{
    readonly IDictionary<string, IWindowHandle> _windows = new Dictionary<string, IWindowHandle>();

    public void FocusOrCreateWindow(
        string key,
        string title,
        Func<IWindowHandle, UIElement> createView,
        bool shrinkToContent = true,
        Size? size = null,
        Action<VatSysForm>? configureForm = null)
    {
        if (_windows.TryGetValue(key, out var existingWindowHandle))
        {
            existingWindowHandle.Focus();
            return;
        }

        var form = new VatSysForm
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent
        };

        var windowHandle = new WindowHandle(form);
        var child = createView(windowHandle);

        var elementHost = new ElementHost();
        if (shrinkToContent)
        {
            // For text wrapping to work correctly, we need to measure with a constraint
            var maxWidth = 520; // Maximum dialog width
            child.Measure(new Size(maxWidth, double.PositiveInfinity));
            child.Arrange(new Rect(child.DesiredSize));
            child.UpdateLayout();

            var desired = child.DesiredSize;
            elementHost.Child = child;
            elementHost.Size = new System.Drawing.Size((int)Math.Ceiling(desired.Width), (int)Math.Ceiling(desired.Height));
            elementHost.Location = new System.Drawing.Point(0, 0);

            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.ClientSize = elementHost.Size;
        }
        else
        {
            elementHost.Child = child;
            elementHost.Dock = DockStyle.Fill;
        }

        form.Controls.Add(elementHost);
        form.Padding = new Padding(0);
        form.Margin = new Padding(0);
        if (size.HasValue)
        {
            form.Width = (int)size.Value.Width;
            form.Height = (int)size.Value.Height;
        }

        form.PerformLayout();
        form.Closed += (_, _) => RemoveWindow(key);

        guiInvoker.InvokeOnUiThread(mainForm => form.Show(mainForm));

        configureForm?.Invoke(form);

        _windows[key] = windowHandle;
    }

    public bool TryGetWindow(string key, out IWindowHandle? windowHandle)
    {
        return _windows.TryGetValue(key, out windowHandle);
    }

    void RemoveWindow(string key)
    {
        _windows.Remove(key);
    }
}
