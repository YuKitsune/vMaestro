using System.Windows;
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

    public VatSysForm(string title, Func<IWindowHandle, UIElement> childFactory, bool shrinkToContent)
    {
        WindowHandle = new WindowHandle(this);

        Text = title;

        var child = childFactory(WindowHandle);
        var elementHost = new ElementHost();
        elementHost.Child = child;
        elementHost.Dock = DockStyle.Fill;

        if (shrinkToContent)
        {
            // Measure the content once to get its natural size
            child.Measure(new Size(520, double.PositiveInfinity));
            child.Arrange(new Rect(child.DesiredSize));
            child.UpdateLayout();

            var contentSize = child.DesiredSize;
            ClientSize = new System.Drawing.Size((int)Math.Ceiling(contentSize.Width), (int)Math.Ceiling(contentSize.Height));

            // Make it resizable so users can adjust if needed
            FormBorderStyle = FormBorderStyle.Sizable;
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
