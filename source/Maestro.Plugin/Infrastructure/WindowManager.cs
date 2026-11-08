using System.Windows;
using System.Windows.Forms;
using Maestro.Wpf.Integrations;

namespace Maestro.Plugin.Infrastructure;

public class WindowManager(GuiInvoker guiInvoker)
{
    readonly IDictionary<string, VatSysForm> _windows = new Dictionary<string, VatSysForm>();
    readonly IDictionary<string, System.Drawing.Point> _lastLocations = new Dictionary<string, System.Drawing.Point>();

    public void FocusOrCreateWindow(
        string key,
        string title,
        Func<IWindowHandle, FrameworkElement> createView,
        bool shrinkToContent = true,
        Size? size = null,
        Action<VatSysForm>? configureForm = null)
    {
        guiInvoker.InvokeOnUiThread(mainForm =>
        {
            if (_windows.TryGetValue(key, out var existingWindowHandle))
            {
                existingWindowHandle.Focus();
                return;
            }

            var form = new VatSysForm(title, createView, shrinkToContent)
            {
                StartPosition = FormStartPosition.CenterParent
            };

            if (_lastLocations.TryGetValue(key, out var lastLocation))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = lastLocation;
            }

            if (size.HasValue)
            {
                form.Width = (int)size.Value.Width;
                form.Height = (int)size.Value.Height;
            }

            form.PerformLayout();
            form.Closed += (_, _) =>
            {
                // If the window is minimised or maximised, Location is not the normal position of the window
                _lastLocations[key] = form.WindowState == FormWindowState.Normal
                    ? form.Location
                    : form.RestoreBounds.Location;

                RemoveWindow(key);
            };
            form.Show(mainForm);

            configureForm?.Invoke(form);

            _windows[key] = form;
        });
    }

    public bool TryGetWindow(string key, out IWindowHandle? windowHandle)
    {
        if (_windows.TryGetValue(key, out var form))
        {
            windowHandle = form.WindowHandle;
            return true;
        }

        windowHandle = null;
        return false;
    }

    void RemoveWindow(string key)
    {
        _windows.Remove(key);
    }
}
