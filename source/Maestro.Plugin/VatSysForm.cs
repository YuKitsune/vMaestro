using System.Windows.Forms;
using Avalonia.Controls;
using Avalonia.Win32.Interoperability;
using Maestro.Avalonia.Integrations;
using vatsys;
using Control = Avalonia.Controls.Control;

namespace Maestro.Plugin;

public class VatSysForm : BaseForm
{
    public event FormClosingEventHandler? CustomFormClosing;

    bool ForceClosing { get; set; }

    public IWindowHandle WindowHandle { get; }

    public void ForceClose()
    {
        ForceClosing = true;
        Close();
    }

    public VatSysForm(string title, Func<IWindowHandle, Control> childFactory, bool shrinkToContent)
    {
        MiddleClickClose = false; // This is on by default. Why...

        Text = title;

        WindowHandle = new WindowHandle(this);
        var child = childFactory(WindowHandle);

        var elementHost = new WinFormsAvaloniaControlHost();
        elementHost.Content = child;
        elementHost.Dock = DockStyle.Fill;

        if (shrinkToContent)
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;

            // Avalonia controls return zero bounds before being attached to the visual tree,
            // so resize the form after the first layout pass instead of pre-measuring.
            void OnLayoutUpdated(object? sender, EventArgs e)
            {
                child.LayoutUpdated -= OnLayoutUpdated;
                var bounds = child.Bounds;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    var size = new System.Drawing.Size(
                        (int)Math.Ceiling(bounds.Width),
                        (int)Math.Ceiling(bounds.Height));
                    elementHost.Size = size;
                    elementHost.Location = System.Drawing.Point.Empty;
                    ClientSize = size;
                }
            }
            child.LayoutUpdated += OnLayoutUpdated;
        }

        Controls.Add(elementHost);

        Padding = new Padding(0);
        Margin = new Padding(0);
        PerformLayout();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ForceClosing && CustomFormClosing != null)
        {
            CustomFormClosing.Invoke(this, e);
            if (e.Cancel)
                return;
        }

        base.OnFormClosing(e);
    }
}
