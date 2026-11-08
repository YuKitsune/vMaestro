using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Forms;
using Maestro.Wpf.Integrations;
using vatsys;

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

    public VatSysForm(string title, Func<IWindowHandle, FrameworkElement> childFactory, bool shrinkToContent)
    {
        MiddleClickClose = false; // This is on by default. Why...

        Text = title;

        WindowHandle = new WindowHandle(this);
        var child = childFactory(WindowHandle);

        var elementHost = new ElementHost
        {
            Dock = DockStyle.Fill
        };

        if (shrinkToContent)
        {
            // A Canvas measures its child with no size limit. The view then always has its natural size,
            // and SizeChanged occurs each time the content changes size.
            var canvas = new Canvas();
            canvas.Children.Add(child);
            elementHost.Child = canvas;

            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            ResizeToContent(child.DesiredSize);
            child.SizeChanged += (_, e) => ResizeToContent(e.NewSize);

            FormBorderStyle = FormBorderStyle.FixedSingle;
        }
        else
        {
            elementHost.Child = child;

            FormBorderStyle = FormBorderStyle.Sizable;
            Resizeable = true;
        }

        Controls.Add(elementHost);

        Padding = new Padding(0);
        Margin = new Padding(0);
    }

    void ResizeToContent(Size contentSize)
    {
        if (WindowState != FormWindowState.Normal)
            return;

        // vatSys draws its own border and title bar. The ClientSize setter uses the standard Windows border to
        // calculate the form size, so it can give the wrong size. Use the real size of the border and title bar.
        var borderWidth = Width - ClientSize.Width;
        var borderHeight = Height - ClientSize.Height;

        Size = new System.Drawing.Size(
            (int)Math.Ceiling(contentSize.Width) + borderWidth,
            (int)Math.Ceiling(contentSize.Height) + borderHeight);
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
